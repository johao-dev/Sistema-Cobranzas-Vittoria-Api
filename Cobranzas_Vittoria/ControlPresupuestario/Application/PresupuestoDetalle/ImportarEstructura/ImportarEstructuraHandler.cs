using Cobranzas_Vittoria.Application.Importacion;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.ImportarEstructura;

public sealed record ImportarEstructuraCommand(int IdPresupuesto, int IdPresupuestoVersion, ArchivoTabular Archivo,
    bool QuitarAusentes);

public sealed record ImportarEstructuraResult(int IdPresupuestoVersion, int Agregados, int Actualizados, int Eliminados,
    int PartidasEnVersion, decimal MontoTotal, int PartidasCreadas);

/// <summary>
/// Importa un presupuesto jerárquico (categorías y partidas finales) sobre una versión BORRADOR: crea en
/// el catálogo las partidas que faltan y carga los montos de las hojas. Una partida que ya existe se
/// reutiliza si coincide en nombre y padre. Todo o nada: con un error en cualquier fila no se crea ni se
/// carga nada y se informan todos los errores juntos (422). Formato en <see cref="EstructuraArchivo"/>.
/// </summary>
public sealed class ImportarEstructuraHandler
{
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;
    private readonly ICatalogoPartidaRepository _partidas;
    private readonly ICatalogoControlPresupuestarioRepository _catalogos;
    private readonly ILectorArchivoTabular _lector;
    private readonly IUsuarioActualService _usuarioActual;
    private readonly ILogger<ImportarEstructuraHandler> _logger;

    public ImportarEstructuraHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles,
        ICatalogoPartidaRepository partidas, ICatalogoControlPresupuestarioRepository catalogos, ILectorArchivoTabular lector,
        IUsuarioActualService usuarioActual, ILogger<ImportarEstructuraHandler> logger)
    {
        _versiones = versiones;
        _detalles = detalles;
        _partidas = partidas;
        _catalogos = catalogos;
        _lector = lector;
        _usuarioActual = usuarioActual;
        _logger = logger;
    }

    public async Task<ImportarEstructuraResult> HandleAsync(ImportarEstructuraCommand command)
    {
        PresupuestoVersionValidator.ValidarIds(command.IdPresupuesto, command.IdPresupuestoVersion);
        var version = await _versiones.ObtenerDelPresupuestoAsync(command.IdPresupuesto, command.IdPresupuestoVersion);
        if (!version.EsBorrador)
            throw new ValidacionPresupuestariaException("VERSION_NO_EDITABLE",
                "Solo se puede importar en una versión en BORRADOR. Crea una nueva versión para modificar montos.");

        var (filas, errores) = EstructuraArchivo.Leer(_lector.Leer(command.Archivo));
        var catalogo = (await _partidas.ListarAsync(new FiltroPartidas()))
            .ToDictionary(p => p.Codigo, StringComparer.OrdinalIgnoreCase);
        var tipos = await _catalogos.ListarTiposPartidaAsync(true);
        var secciones = await _catalogos.ListarSeccionesGastoAsync(true);
        var enArchivo = filas.GroupBy(f => f.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Tipo de una partida nueva: el suyo o el del ancestro más cercano (del archivo o del catálogo).
        var tipoResuelto = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        int? TipoDe(string codigo, int profundidad = 0)
        {
            if (tipoResuelto.TryGetValue(codigo, out var memo)) return memo;
            int? id = null;
            if (catalogo.TryGetValue(codigo, out var existente)) id = existente.IdTipoPartida;
            else if (profundidad < 64 && enArchivo.TryGetValue(codigo, out var fila))
                id = fila.Tipo is not null
                    ? tipos.FirstOrDefault(t => Coincide(t.Codigo, t.Nombre, fila.Tipo))?.IdTipoPartida
                    : fila.CodigoPadre is null ? null : TipoDe(fila.CodigoPadre, profundidad + 1);
            return tipoResuelto[codigo] = id;
        }

        var nuevas = new List<PartidaNuevaEstructura>();
        var montos = new List<MontoEstructura>();
        foreach (var fila in filas.Where(f => enArchivo[f.Codigo] == f))
        {
            var n = fila.Fila;
            catalogo.TryGetValue(fila.Codigo, out var existente);
            var esCategoria = fila.EsCategoria || existente is { EsHoja: false };

            if (existente is not null)
            {
                if (!EstructuraArchivo.MismoNombre(existente.Nombre, fila.Nombre))
                    errores.Add(new(n, "Nombre", EstructuraArchivo.NombreDistinto,
                        $"La partida {fila.Codigo} ya existe en el catálogo como '{existente.Nombre}'. Usa ese nombre o un código distinto."));
                if (!string.Equals(existente.CodigoPartidaPadre, fila.CodigoPadre, StringComparison.OrdinalIgnoreCase))
                    errores.Add(new(n, "Codigo", EstructuraArchivo.JerarquiaDistinta,
                        $"En el catálogo la partida {fila.Codigo} " + (existente.CodigoPartidaPadre is null
                            ? "es una categoría raíz" : $"cuelga de {existente.CodigoPartidaPadre}") +
                        (fila.CodigoPadre is null ? ", pero el archivo la trae como raíz." : $", pero el archivo la ubica bajo {fila.CodigoPadre}.")));
                if (!existente.Activo)
                    errores.Add(new(n, "Codigo", CodigosError.Fila.ReglaNegocio, $"La partida {fila.Codigo} está inactiva en el catálogo."));
                if (!fila.EsCategoria && esCategoria && fila.Monto is > 0)
                    errores.Add(new(n, "Monto", EstructuraArchivo.PadreConMonto,
                        $"La partida {fila.Codigo} tiene partidas hijas en el catálogo: deja el monto vacío o en 0."));
            }
            else
            {
                if (fila.CodigoPadre is not null && !enArchivo.ContainsKey(fila.CodigoPadre) && !catalogo.ContainsKey(fila.CodigoPadre))
                    errores.Add(new(n, "Codigo", EstructuraArchivo.PadreNoExiste,
                        $"La categoría {fila.CodigoPadre} de la partida {fila.Codigo} no está en el archivo ni en el catálogo."));

                var tipo = TipoDe(fila.Codigo);
                if (fila.Tipo is not null && tipo is null)
                    errores.Add(new(n, "Tipo", CodigosError.Sp.FkNoExiste,
                        $"El tipo '{fila.Tipo}' no existe. Usa uno de: {string.Join(", ", tipos.Select(t => t.Codigo))}."));
                else if (tipo is null)
                    errores.Add(new(n, "Tipo", CodigosError.Fila.CampoRequerido,
                        $"Indica el Tipo de la partida {fila.Codigo} o de su categoría raíz; las hijas lo heredan."));

                int? idSeccion = null;
                if (fila.Seccion is not null && !esCategoria)
                {
                    idSeccion = secciones.FirstOrDefault(s => Coincide(s.Codigo, s.Nombre, fila.Seccion))?.IdSeccionGasto;
                    if (idSeccion is null)
                        errores.Add(new(n, "Seccion", CodigosError.Sp.FkNoExiste,
                            $"La sección '{fila.Seccion}' no existe. Usa una de: {string.Join(", ", secciones.Select(s => s.Codigo))}."));
                }
                nuevas.Add(new PartidaNuevaEstructura(fila.Codigo, fila.Nombre, tipo ?? 0, fila.CodigoPadre, idSeccion, n));
            }

            if (!esCategoria)
            {
                if (fila.MontoTexto is null)
                    errores.Add(new(n, "Monto", CodigosError.Fila.CampoRequerido,
                        $"La partida {fila.Codigo} no tiene hijas: el monto es obligatorio. Si no aplica, escribe 0."));
                else if (fila.Monto is decimal monto)
                    montos.Add(new MontoEstructura(fila.Codigo, monto, fila.Observacion, n));
            }
        }

        if (errores.Count == 0 && montos.Count == 0)
            errores.Add(new(0, "Monto", CodigosError.Fila.ReglaNegocio, "El archivo no trae ninguna partida final (sin hijas) con monto."));
        if (errores.Count > 0)
            throw new DatosInvalidosException($"El archivo contiene {errores.Count} error(es). No se creó ninguna partida ni se cargó ningún monto.",
                errores.OrderBy(e => e.Fila).ThenBy(e => e.Campo).ToList());

        var resultado = await _detalles.ImportarEstructuraAsync(new ImportacionEstructura(command.IdPresupuestoVersion, nuevas,
            montos, command.QuitarAusentes, _usuarioActual.ObtenerUsuarioActual()));
        _logger.LogInformation("Estructura importada en la versión {Version}: {Creadas} partidas nuevas, {Partidas} con monto, total {Total}",
            command.IdPresupuestoVersion, resultado.PartidasCreadas, resultado.Lote.PartidasEnVersion, resultado.Lote.MontoTotal);
        var lote = resultado.Lote;
        return new ImportarEstructuraResult(lote.IdPresupuestoVersion, lote.Agregados, lote.Actualizados, lote.Eliminados,
            lote.PartidasEnVersion, lote.MontoTotal, resultado.PartidasCreadas);
    }

    /// <summary>Un tipo o sección se indica por código (MANO_OBRA) o por nombre (Mano de obra), sin tildes ni mayúsculas.</summary>
    private static bool Coincide(string codigo, string nombre, string valor)
        => string.Equals(codigo, valor.Trim().Replace(' ', '_'), StringComparison.OrdinalIgnoreCase)
            || EstructuraArchivo.MismoNombre(nombre, valor);
}
