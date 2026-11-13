using Cobranzas_Vittoria.Application.Common.Eventos;
using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Contracts.Eventos;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using VersionPresupuesto = Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.PresupuestoVersion;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.Aprobar;

/// <summary>
/// Aprobar es una acción de negocio: el SP valida, bloquea y en una sola transacción pasa la
/// versión APROBADA anterior a HISTORICO y esta de BORRADOR a APROBADO.
/// </summary>
public sealed class AprobarVersionHandler
{
    private readonly IPresupuestoVersionRepository _repository;
    private readonly IUsuarioActualService _usuarioActual;
    private readonly ILogger<AprobarVersionHandler> _logger;
    private readonly IEventPublisher _eventPublisher;
    private readonly TimeProvider _reloj;

    public AprobarVersionHandler(IPresupuestoVersionRepository repository, IUsuarioActualService usuarioActual,
        ILogger<AprobarVersionHandler> logger, IEventPublisher eventPublisher, TimeProvider reloj)
    {
        _repository = repository;
        _usuarioActual = usuarioActual;
        _logger = logger;
        _eventPublisher = eventPublisher;
        _reloj = reloj;
    }

    public async Task<PresupuestoVersionResult> HandleAsync(AprobarVersionCommand command)
    {
        PresupuestoVersionValidator.ValidarIds(command.IdPresupuesto, command.IdPresupuestoVersion);
        await _repository.ObtenerDelPresupuestoAsync(command.IdPresupuesto, command.IdPresupuestoVersion);
        VersionAprobada resultado = await _repository.AprobarAsync(
            command.IdPresupuestoVersion, _usuarioActual.ObtenerUsuarioActual());

        _logger.LogInformation("Versión aprobada: IdPresupuesto={Presupuesto}, IdVersion={Version}",
            command.IdPresupuesto, command.IdPresupuestoVersion);

        PresupuestoAprobadoEvent evento = new(Guid.NewGuid(), resultado.IdPresupuesto,
            resultado.IdPresupuestoVersion, resultado.NumeroVersion, resultado.FechaAprobacionUtc,
            _reloj.GetUtcNow().UtcDateTime);

        try
        {
            await _eventPublisher.PublishAsync(evento, CancellationToken.None);
        }
        catch (Exception excepcion)
        {
            _logger.LogError(
                "Falló la publicación del evento {IdEvento} de la versión confirmada {IdPresupuestoVersion}. Tipo: {TipoError}",
                evento.IdEvento, evento.IdPresupuestoVersion, excepcion.GetType().Name);
        }

        VersionPresupuesto aprobada = await _repository.ObtenerDelPresupuestoAsync(
            command.IdPresupuesto, command.IdPresupuestoVersion);
        return PresupuestoVersionResult.Desde(aprobada);
    }
}
