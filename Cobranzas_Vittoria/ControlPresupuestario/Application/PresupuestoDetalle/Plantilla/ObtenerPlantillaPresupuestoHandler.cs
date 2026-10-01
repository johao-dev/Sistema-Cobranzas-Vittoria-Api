using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Plantilla;

/// <summary>
/// Plantilla de carga de una versión: todas las partidas hoja activas con el monto actual de la
/// versión (0 si no la tiene), lista para completar y volver a subir.
/// </summary>
public sealed class ObtenerPlantillaPresupuestoHandler
{
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;
    private readonly ICatalogoPartidaRepository _partidas;

    public ObtenerPlantillaPresupuestoHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles,
        ICatalogoPartidaRepository partidas)
    {
        _versiones = versiones;
        _detalles = detalles;
        _partidas = partidas;
    }

    public async Task<IReadOnlyList<PlantillaPresupuestoFila>> HandleAsync(ObtenerPlantillaPresupuestoQuery query)
    {
        PresupuestoVersionValidator.ValidarIds(query.IdPresupuesto, query.IdPresupuestoVersion);
        await _versiones.ObtenerDelPresupuestoAsync(query.IdPresupuesto, query.IdPresupuestoVersion);
        var montos = (await _detalles.ListarPorVersionAsync(query.IdPresupuestoVersion)).ToDictionary(d => d.IdCatalogoPartida);
        var hojas = await _partidas.ListarAsync(new FiltroPartidas(Activo: true, EsHoja: true));
        return hojas
            .OrderBy(p => p.Codigo, StringComparer.Ordinal)
            .Select(p => montos.TryGetValue(p.IdCatalogoPartida, out var d)
                ? new PlantillaPresupuestoFila(p.Codigo, p.Nombre, d.MontoPresupuestado, d.Observacion)
                : new PlantillaPresupuestoFila(p.Codigo, p.Nombre, 0m, null))
            .ToList();
    }
}
