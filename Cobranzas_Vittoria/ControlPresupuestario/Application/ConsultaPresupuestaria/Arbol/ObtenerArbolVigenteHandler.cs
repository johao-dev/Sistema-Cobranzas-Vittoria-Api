using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Arbol;

public sealed record ObtenerArbolVigenteQuery(int IdCentroCosto, int? IdPresupuesto);

/// <summary>
/// Árbol de seguimiento de un centro de costo: los montos vigentes (vw_ControlPresupuestarioVigente)
/// de sus presupuestos activos con versión aprobada, con las mismas reglas de selección que el tablero.
/// Nunca se suman montos de monedas distintas.
/// </summary>
public sealed class ObtenerArbolVigenteHandler
{
    private readonly ICentroCostoRepository _centros;
    private readonly IPresupuestoRepository _presupuestos;
    private readonly IConsultaPresupuestariaRepository _consultas;
    private readonly ICatalogoPartidaRepository _partidas;

    public ObtenerArbolVigenteHandler(ICentroCostoRepository centros, IPresupuestoRepository presupuestos,
        IConsultaPresupuestariaRepository consultas, ICatalogoPartidaRepository partidas)
    {
        _centros = centros;
        _presupuestos = presupuestos;
        _consultas = consultas;
        _partidas = partidas;
    }

    public async Task<ArbolPresupuestarioResult> HandleAsync(ObtenerArbolVigenteQuery query)
    {
        if (query.IdCentroCosto <= 0)
            throw new ValidacionPresupuestariaException("CAMPO_REQUERIDO", "Selecciona un centro de costo para ver el árbol de partidas.");
        if (query.IdPresupuesto is <= 0)
            throw new ValidacionPresupuestariaException("IDENTIFICADOR_INVALIDO", "El presupuesto indicado no es válido.");

        var centro = await _centros.ObtenerAsync(query.IdCentroCosto)
            ?? throw new CentroCostoNoEncontradoException(query.IdCentroCosto);
        var presupuestos = (await _presupuestos.ListarAsync(true, query.IdCentroCosto, null, null))
            .Where(p => p.TieneVersionAprobada && (query.IdPresupuesto is null || p.IdPresupuesto == query.IdPresupuesto))
            .ToList();
        if (presupuestos.Select(p => p.IdMoneda).Distinct().Count() > 1)
            throw new ValidacionPresupuestariaException("MONEDAS_MIXTAS",
                "El centro de costo tiene presupuestos en distintas monedas: elige un presupuesto para ver el árbol.");

        var ids = presupuestos.Select(p => p.IdPresupuesto).ToHashSet();
        var filas = ids.Count == 0 ? Array.Empty<SaldoPartidaFila>()
            : (await _consultas.VigenteAsync(new FiltroConsultaPresupuestaria(IdCentroCosto: query.IdCentroCosto,
                IdPresupuesto: query.IdPresupuesto))).Where(f => ids.Contains(f.IdPresupuesto)).ToArray();

        var moneda = presupuestos.OrderBy(p => p.IdMoneda).FirstOrDefault();
        var unico = presupuestos.Count == 1 ? presupuestos[0] : null;
        var encabezado = new EncabezadoArbol(centro.IdCentroCosto, centro.Codigo, centro.Nombre, unico?.IdPresupuesto,
            unico?.IdPresupuestoVersionAprobada, unico is null ? null : "APROBADO", moneda?.CodigoMoneda, moneda?.SimboloMoneda);
        return await ArbolPresupuestarioResult.ConstruirAsync(encabezado, filas, _partidas);
    }
}
