using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Agregar;

public sealed class AgregarPresupuestoDetalleHandler
{
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;
    private readonly ILogger<AgregarPresupuestoDetalleHandler> _logger;

    public AgregarPresupuestoDetalleHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles,
        ILogger<AgregarPresupuestoDetalleHandler> logger)
    {
        _versiones = versiones;
        _detalles = detalles;
        _logger = logger;
    }

    public async Task<PresupuestoDetalleResult> HandleAsync(AgregarPresupuestoDetalleCommand command)
    {
        PresupuestoVersionValidator.ValidarIds(command.IdPresupuesto, command.IdPresupuestoVersion);
        await _versiones.ObtenerDelPresupuestoAsync(command.IdPresupuesto, command.IdPresupuestoVersion);
        var detalle = Domain.Model.PresupuestoDetalle.Crear(command.IdPresupuestoVersion, command.IdCatalogoPartida,
            command.MontoPresupuestado, command.Observacion);
        var id = await _detalles.AgregarAsync(detalle);
        _logger.LogInformation("Partida agregada a la versión {Version}: IdPresupuestoDetalle={Id}", command.IdPresupuestoVersion, id);
        return PresupuestoDetalleResult.Desde(await _detalles.ObtenerDeLaVersionAsync(command.IdPresupuestoVersion, id));
    }
}
