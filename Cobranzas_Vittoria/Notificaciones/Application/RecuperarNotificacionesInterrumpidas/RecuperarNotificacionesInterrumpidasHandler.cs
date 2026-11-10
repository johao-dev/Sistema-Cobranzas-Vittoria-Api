using Cobranzas_Vittoria.Notificaciones.Application.Options;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;
using Microsoft.Extensions.Options;

namespace Cobranzas_Vittoria.Notificaciones.Application.RecuperarNotificacionesInterrumpidas;

/// <summary>Recupera intentos vencidos sin asumir que el proveedor rechazó el correo.</summary>
public sealed class RecuperarNotificacionesInterrumpidasHandler
{
    private readonly INotificacionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RecuperarNotificacionesInterrumpidasHandler> _logger;
    private readonly int _tamanoLote;
    private readonly int _maximoIntentos;
    private readonly TimeSpan _intervaloReintento;
    private readonly TimeSpan _timeoutProcesamiento;

    public RecuperarNotificacionesInterrumpidasHandler(
        INotificacionRepository repository,
        IOptions<NotificacionProcesamientoOptions> options,
        TimeProvider timeProvider,
        ILogger<RecuperarNotificacionesInterrumpidasHandler> logger)
    {
        NotificacionProcesamientoOptions configuracion = options.Value;
        configuracion.Validar();

        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
        _tamanoLote = configuracion.TamanoLote;
        _maximoIntentos = configuracion.MaximoIntentos;
        _intervaloReintento = TimeSpan.FromMinutes(configuracion.IntervaloReintentoMinutos);
        _timeoutProcesamiento = TimeSpan.FromMinutes(configuracion.TimeoutProcesamientoMinutos);
    }

    public async Task<ResultadoProcesamientoLote> HandleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTime ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime fechaLimiteUtc = ahoraUtc.Subtract(_timeoutProcesamiento);

        IReadOnlyCollection<Notificacion> notificaciones = await _repository.ObtenerProcesamientosVencidosAsync(
            _tamanoLote, fechaLimiteUtc, cancellationToken);

        int guardadas = 0;
        int conflictos = 0;

        foreach (Notificacion notificacion in notificaciones)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (notificacion.Estado != Notificacion.EstadoNotificacion.Procesando
                || notificacion.FechaInicioProcesamiento is not DateTime inicioUtc
                || inicioUtc >= fechaLimiteUtc)
            {
                continue;
            }

            int intentoEsperado = notificacion.Intentos;

            // El resultado es incierto: se reintenta con el riesgo documentado de entrega duplicada.
            notificacion.RegistrarFallo("Procesamiento interrumpido; no se confirmó el resultado del envío.",
                ahoraUtc, _maximoIntentos, _intervaloReintento);

            bool guardada = await _repository.GuardarResultadoAsync(notificacion, intentoEsperado, cancellationToken);

            if (guardada)
            {
                guardadas++;
            }
            else
            {
                conflictos++;
                _logger.LogWarning("Conflicto al recuperar notificación {IdNotificacion}, intento {Intento}",
                    notificacion.IdNotificacion, intentoEsperado);
            }
        }

        return new ResultadoProcesamientoLote(notificaciones.Count, guardadas, conflictos);
    }
}
