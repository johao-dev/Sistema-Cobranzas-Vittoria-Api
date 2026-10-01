using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Service;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Dashboard;

/// <summary>
/// Tablero de un centro de costo, compuesto a partir de las vistas: los presupuestos activos con
/// versión aprobada, sus montos vigentes por rubro (vw_ControlPresupuestarioVigente) y la ejecución
/// diaria (vw_EjecucionDiariaPorPartida). Nunca se suman montos de monedas distintas.
/// </summary>
public sealed class ObtenerDashboardHandler
{
    private readonly ICentroCostoRepository _centros;
    private readonly IPresupuestoRepository _presupuestos;
    private readonly IConsultaPresupuestariaRepository _consultas;
    private readonly ICatalogoPartidaRepository _partidas;
    private readonly TimeProvider _reloj;

    public ObtenerDashboardHandler(ICentroCostoRepository centros, IPresupuestoRepository presupuestos,
        IConsultaPresupuestariaRepository consultas, ICatalogoPartidaRepository partidas, TimeProvider reloj)
    {
        _centros = centros;
        _presupuestos = presupuestos;
        _consultas = consultas;
        _partidas = partidas;
        _reloj = reloj;
    }

    public async Task<Tablero> HandleAsync(ObtenerDashboardQuery query)
    {
        if (query.IdCentroCosto <= 0)
            throw new ValidacionPresupuestariaException("CAMPO_REQUERIDO", "Selecciona un centro de costo para ver el dashboard.");
        if (query.IdPresupuesto is <= 0)
            throw new ValidacionPresupuestariaException("IDENTIFICADOR_INVALIDO", "El presupuesto indicado no es válido.");
        if (query.Nivel is <= 0)
            throw new ValidacionPresupuestariaException("NIVEL_INVALIDO", "El nivel de anidamiento empieza en 1.");
        if (query.IdPartidaPadre is <= 0)
            throw new ValidacionPresupuestariaException("IDENTIFICADOR_INVALIDO", "La partida indicada no es válida.");

        var centro = await _centros.ObtenerAsync(query.IdCentroCosto)
            ?? throw new CentroCostoNoEncontradoException(query.IdCentroCosto);
        var presupuestos = (await _presupuestos.ListarAsync(true, query.IdCentroCosto, null, null))
            .Where(p => p.TieneVersionAprobada && (query.IdPresupuesto is null || p.IdPresupuesto == query.IdPresupuesto))
            .ToList();
        if (presupuestos.Select(p => p.IdMoneda).Distinct().Count() > 1)
            throw new ValidacionPresupuestariaException("MONEDAS_MIXTAS",
                "El centro de costo tiene presupuestos en distintas monedas: elige un presupuesto para ver el dashboard.");

        var ids = presupuestos.Select(p => p.IdPresupuesto).ToHashSet();
        var vigente = ids.Count == 0 ? Array.Empty<SaldoPartidaFila>()
            : await _consultas.VigenteAsync(new FiltroConsultaPresupuestaria(IdCentroCosto: query.IdCentroCosto,
                IdPresupuesto: query.IdPresupuesto));
        // Cada partida final se agrupa en su ancestra del nivel pedido; una rama menos profunda
        // que ese nivel se queda en su propia hoja. Sin nivel, el rubro es la partida final.
        var catalogo = (await _partidas.ListarAsync(new FiltroPartidas())).ToDictionary(p => p.IdCatalogoPartida);
        Domain.Model.CatalogoPartida? rama = null;
        if (query.IdPartidaPadre is int idRama && !catalogo.TryGetValue(idRama, out rama))
            throw new CatalogoPartidaNoEncontradoException(idRama);
        // Dentro de una rama, sin nivel explícito se agrupa por sus hijas directas.
        var nivelAgrupacion = query.Nivel ?? (rama is null ? null : rama.Nivel + 1);

        IEnumerable<Domain.Model.CatalogoPartida> Camino(int idPartida)
        {
            for (var actual = catalogo.GetValueOrDefault(idPartida); actual is not null;
                 actual = actual.IdPartidaPadre is int padre ? catalogo.GetValueOrDefault(padre) : null)
                yield return actual;
        }
        bool EnRama(int idPartida) => rama is null || Camino(idPartida).Any(p => p.IdCatalogoPartida == rama.IdCatalogoPartida);
        Domain.Model.CatalogoPartida? Rubro(int idPartida)
        {
            var camino = Camino(idPartida).ToList();
            if (camino.Count == 0) return null;
            return nivelAgrupacion is int nivel ? camino.LastOrDefault(p => p.Nivel >= nivel) ?? camino[0] : camino[0];
        }
        var filas = vigente.Where(f => ids.Contains(f.IdPresupuesto) && EnRama(f.IdCatalogoPartida)).ToList();
        var nivelMaximo = filas.Select(f => catalogo.TryGetValue(f.IdCatalogoPartida, out var p) ? p.Nivel : 1)
            .DefaultIfEmpty(0).Max();
        var rubros = filas
            .GroupBy(f => Rubro(f.IdCatalogoPartida) is { } r
                ? (r.IdCatalogoPartida, (string?)r.Codigo, (string?)r.Nombre)
                : (f.IdCatalogoPartida, f.CodigoPartida, f.NombrePartida))
            .OrderBy(g => g.Key.Item2, Comparer<string?>.Create((a, b) => ArbolPresupuestario.CompararCodigos(a, b)))
            .Select(g => new RubroTablero(g.Key.Item1, g.Key.Item2 ?? string.Empty,
                g.Key.Item3 ?? string.Empty, g.Sum(f => f.MontoPresupuestado), g.Sum(f => f.MontoComprometido),
                g.Sum(f => f.MontoEjecutado)))
            .ToList();
        var ejecuciones = (await _consultas.EjecucionDiariaAsync(ids))
            .Where(e => EnRama(e.IdCatalogoPartida))
            .GroupBy(e => (e.Fecha.Date, IdCatalogoPartida: Rubro(e.IdCatalogoPartida)?.IdCatalogoPartida ?? e.IdCatalogoPartida))
            .Select(g => new EjecucionTablero(g.Key.Date, g.Key.IdCatalogoPartida, g.Sum(e => e.MontoEjecutado)))
            .ToList();

        var moneda = presupuestos.OrderBy(p => p.IdMoneda).FirstOrDefault();
        var encabezado = new EncabezadoTablero(centro.IdCentroCosto, centro.Codigo, centro.Nombre, centro.NombreProyecto,
            moneda?.IdMoneda, moneda?.CodigoMoneda, moneda?.SimboloMoneda, presupuestos.Select(p => p.IdMoneda).Distinct().Count(),
            presupuestos.Count, presupuestos.Min(p => p.FechaInicio), presupuestos.Max(p => p.FechaFin));
        var tablero = TableroPresupuestario.Construir(encabezado, rubros, ejecuciones, _reloj.GetLocalNow().DateTime);
        return tablero with
        {
            Encabezado = tablero.Encabezado with
            {
                Nivel = nivelAgrupacion is int n ? Math.Min(n, Math.Max(nivelMaximo, 1)) : null,
                NivelMaximo = nivelMaximo,
                Rama = rama is null ? Array.Empty<TableroRama>()
                    : Camino(rama.IdCatalogoPartida).Reverse()
                        .Select(p => new TableroRama(p.IdCatalogoPartida, p.Codigo, p.Nombre, p.Nivel)).ToList()
            }
        };
    }
}
