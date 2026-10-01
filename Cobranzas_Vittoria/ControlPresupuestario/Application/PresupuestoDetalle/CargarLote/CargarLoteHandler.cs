using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.CargarLote;

/// <summary>Carga completa de montos sobre una versión BORRADOR (formulario con todas las partidas), todo o nada.</summary>
public sealed class CargarLoteHandler
{
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;
    private readonly ILogger<CargarLoteHandler> _logger;

    public CargarLoteHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles,
        ILogger<CargarLoteHandler> logger)
    {
        _versiones = versiones;
        _detalles = detalles;
        _logger = logger;
    }

    public async Task<CargaLoteResult> HandleAsync(CargarLoteCommand command)
    {
        PresupuestoVersionValidator.ValidarIds(command.IdPresupuesto, command.IdPresupuestoVersion);
        await _versiones.ObtenerDelPresupuestoAsync(command.IdPresupuesto, command.IdPresupuestoVersion);
        var lote = LotePresupuestario.Crear(command.IdPresupuestoVersion,
            (command.Detalles ?? Array.Empty<CargarLoteItem>())
                .Select(d => new LotePresupuestario.Item(d.IdCatalogoPartida, d.MontoPresupuestado, d.Observacion, 0)),
            command.QuitarAusentes);
        var resultado = await _detalles.CargarLoteAsync(lote);
        _logger.LogInformation("Carga completa en la versión {Version}: {Agregados} agregadas, {Actualizados} actualizadas, {Eliminados} eliminadas",
            command.IdPresupuestoVersion, resultado.Agregados, resultado.Actualizados, resultado.Eliminados);
        return CargaLoteResult.Desde(resultado);
    }
}
