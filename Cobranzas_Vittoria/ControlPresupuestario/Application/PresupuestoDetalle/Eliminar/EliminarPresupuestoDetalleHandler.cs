using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Eliminar;

public sealed class EliminarPresupuestoDetalleHandler
{
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;
    private readonly ILogger<EliminarPresupuestoDetalleHandler> _logger;

    public EliminarPresupuestoDetalleHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles,
        ILogger<EliminarPresupuestoDetalleHandler> logger)
    {
        _versiones = versiones;
        _detalles = detalles;
        _logger = logger;
    }

    public async Task HandleAsync(EliminarPresupuestoDetalleCommand c)
    {
        PresupuestoDetalleValidator.ValidarIds(c.IdPresupuesto, c.IdPresupuestoVersion, c.IdPresupuestoDetalle);
        await _versiones.ObtenerDelPresupuestoAsync(c.IdPresupuesto, c.IdPresupuestoVersion);
        await _detalles.ObtenerDeLaVersionAsync(c.IdPresupuestoVersion, c.IdPresupuestoDetalle);
        await _detalles.EliminarAsync(c.IdPresupuestoDetalle);
        _logger.LogInformation("Partida de versión eliminada: IdPresupuestoDetalle={Id}", c.IdPresupuestoDetalle);
    }
}
