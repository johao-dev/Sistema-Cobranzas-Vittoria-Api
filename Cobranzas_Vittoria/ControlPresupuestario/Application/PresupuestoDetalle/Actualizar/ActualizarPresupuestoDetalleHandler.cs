using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Actualizar;

public sealed class ActualizarPresupuestoDetalleHandler
{
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;
    private readonly ILogger<ActualizarPresupuestoDetalleHandler> _logger;

    public ActualizarPresupuestoDetalleHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles,
        ILogger<ActualizarPresupuestoDetalleHandler> logger)
    {
        _versiones = versiones;
        _detalles = detalles;
        _logger = logger;
    }

    public async Task<PresupuestoDetalleResult> HandleAsync(ActualizarPresupuestoDetalleCommand c)
    {
        PresupuestoDetalleValidator.ValidarIds(c.IdPresupuesto, c.IdPresupuestoVersion, c.IdPresupuestoDetalle);
        await _versiones.ObtenerDelPresupuestoAsync(c.IdPresupuesto, c.IdPresupuestoVersion);
        var detalle = await _detalles.ObtenerDeLaVersionAsync(c.IdPresupuestoVersion, c.IdPresupuestoDetalle);
        detalle.ActualizarMonto(c.MontoPresupuestado, c.Observacion);
        await _detalles.ActualizarAsync(detalle);
        _logger.LogInformation("Partida de versión actualizada: IdPresupuestoDetalle={Id}", c.IdPresupuestoDetalle);
        return PresupuestoDetalleResult.Desde(await _detalles.ObtenerDeLaVersionAsync(c.IdPresupuestoVersion, c.IdPresupuestoDetalle));
    }
}
