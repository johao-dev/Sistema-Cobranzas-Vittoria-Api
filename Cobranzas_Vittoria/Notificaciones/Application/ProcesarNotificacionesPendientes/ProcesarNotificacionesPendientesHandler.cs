using Cobranzas_Vittoria.Notificaciones.Application.Options;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;
using Microsoft.Extensions.Options;

namespace Cobranzas_Vittoria.Notificaciones.Application.ProcesarNotificacionesPendientes;

/// <summary>Coordina envíos sobre intentos ya reclamados; no conserva una transacción durante el transporte.</summary>
public sealed class ProcesarNotificacionesPendientesHandler
{
    private readonly INotificacionRepository _repository;
    private readonly IEmailSender _emailSender;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProcesarNotificacionesPendientesHandler> _logger;
    private readonly int _tamanoLote;
    private readonly int _maximoIntentos;
    private readonly TimeSpan _intervaloReintento;

    public ProcesarNotificacionesPendientesHandler(
        INotificacionRepository repository,
        IEmailSender emailSender,
        IOptions<NotificacionProcesamientoOptions> options,
        TimeProvider timeProvider,
        ILogger<ProcesarNotificacionesPendientesHandler> logger)
    {
        NotificacionProcesamientoOptions configuracion = options.Value;
        configuracion.Validar();

        _repository = repository;
        _emailSender = emailSender;
        _timeProvider = timeProvider;
        _logger = logger;
        _tamanoLote = configuracion.TamanoLote;
        _maximoIntentos = configuracion.MaximoIntentos;
        _intervaloReintento = TimeSpan.FromMinutes(configuracion.IntervaloReintentoMinutos);
    }

    public async Task<ResultadoProcesamientoLote> HandleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<Notificacion> notificaciones = await _repository.ReclamarPendientesAsync(
            _tamanoLote, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        int guardadas = 0;
        int conflictos = 0;

        foreach (Notificacion notificacion in notificaciones)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int intentoEsperado = notificacion.Intentos;

            SolicitudEnvioCorreo solicitud = new(
                notificacion.AsuntoGenerado,
                notificacion.CuerpoHtmlGenerado,
                ObtenerDirecciones(notificacion, NotificacionDestinatario.TipoEnvioConfig.To),
                ObtenerDirecciones(notificacion, NotificacionDestinatario.TipoEnvioConfig.Cc),
                ObtenerDirecciones(notificacion, NotificacionDestinatario.TipoEnvioConfig.Bcc));

            // Solo el adaptador clasifica fallos de transporte. Persistencia y transiciones quedan fuera del catch.
            EnvioCorreoException? fallo = null;

            try
            {
                await _emailSender.EnviarAsync(solicitud, cancellationToken);
            }
            catch (EnvioCorreoException excepcion) when (!cancellationToken.IsCancellationRequested)
            {
                fallo = excepcion;
            }

            if (fallo is null)
            {
                notificacion.MarcarComoEnviada(_timeProvider.GetUtcNow().UtcDateTime);
            }
            else
            {
                string diagnostico = fallo.EsTransitorio switch
                {
                    true => "Fallo transitorio del transporte de correo.",
                    false => "Fallo permanente del transporte de correo.",
                    null => "Fallo del transporte de correo de clasificación desconocida."
                };

                notificacion.RegistrarFallo(diagnostico, _timeProvider.GetUtcNow().UtcDateTime,
                    _maximoIntentos, _intervaloReintento, permitirReintento: fallo.EsTransitorio != false);

                // No se registra la excepción original: el proveedor puede incluir información sensible.
                _logger.LogWarning("Fallo de transporte en notificación {IdNotificacion}, intento {Intento}, transitorio {EsTransitorio}",
                    notificacion.IdNotificacion, intentoEsperado, fallo.EsTransitorio);
            }

            bool guardada = await _repository.GuardarResultadoAsync(notificacion, intentoEsperado, cancellationToken);

            if (guardada)
            {
                guardadas++;
            }
            else
            {
                conflictos++;
                _logger.LogWarning("Conflicto al guardar notificación {IdNotificacion}, intento {Intento}",
                    notificacion.IdNotificacion, intentoEsperado);
            }
        }

        return new ResultadoProcesamientoLote(notificaciones.Count, guardadas, conflictos);
    }

    private static IReadOnlyCollection<DireccionCorreo> ObtenerDirecciones(
        Notificacion notificacion, NotificacionDestinatario.TipoEnvioConfig tipoEnvio)
    {
        return notificacion.Destinatarios
            .Where(destinatario => destinatario.TipoEnvio == tipoEnvio)
            .Select(destinatario => new DireccionCorreo(destinatario.Correo, destinatario.Nombre))
            .ToList().AsReadOnly();
    }
}
