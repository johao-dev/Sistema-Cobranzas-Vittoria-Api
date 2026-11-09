using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Validator;

/// <summary>Valida los datos necesarios para crear una notificación y registrar sus fallos.</summary>
internal static class NotificacionValidator
{
    private const int LONGITUD_MAXIMA_ASUNTO = 250;

    internal static void ValidarCreacion(
        Guid idEvento,
        int idCodigoEventoNotificacion,
        int idReferencia,
        string asuntoGenerado,
        string cuerpoHtmlGenerado)
    {
        if (idEvento == Guid.Empty)
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_ID_EVENTO_REQUERIDO",
                "El identificador del evento es obligatorio.");
        }

        if (idCodigoEventoNotificacion <= 0)
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_CODIGO_EVENTO_INVALIDO",
                "El identificador del código de evento debe ser positivo.");
        }

        if (idReferencia <= 0)
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_REFERENCIA_INVALIDA",
                "El identificador del registro de referencia debe ser positivo.");
        }

        if (string.IsNullOrWhiteSpace(asuntoGenerado) || asuntoGenerado.Length > LONGITUD_MAXIMA_ASUNTO)
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_ASUNTO_INVALIDO",
                $"El asunto debe contener entre 1 y {LONGITUD_MAXIMA_ASUNTO} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(cuerpoHtmlGenerado))
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_CUERPO_REQUERIDO",
                "El contenido del correo es obligatorio.");
        }
    }

    internal static IReadOnlyCollection<NotificacionDestinatario> ValidarYCopiarDestinatarios(
        int idCodigoEventoNotificacion, IEnumerable<NotificacionDestinatario> destinatarios)
    {
        if (destinatarios is null)
        {
            throw new NotificacionSinDestinatariosException(idCodigoEventoNotificacion);
        }

        List<NotificacionDestinatario> copia = destinatarios.ToList();

        if (copia.Count == 0)
        {
            throw new NotificacionSinDestinatariosException(idCodigoEventoNotificacion);
        }

        HashSet<(string Correo, NotificacionDestinatario.TipoEnvioConfig TipoEnvio)> claves = new();

        foreach (NotificacionDestinatario destinatario in copia)
        {
            if (destinatario is null || destinatario.IdNotificacion != 0 || destinatario.IdNotificacionDestinatario != 0)
            {
                throw new ValidacionNegocioNotificacionException(
                    "NOTIFICACION_DESTINATARIO_NO_NUEVO", "Los destinatarios deben ser nuevos y no pertenecer a una notificación persistida.");
            }

            NotificacionDestinatarioValidator.ValidarCreacion(destinatario.Correo, destinatario.Nombre, destinatario.TipoEnvio);

            // No repetimos la misma dirección y participación; los distintos tipos de envío siguen siendo explícitos.
            if (!claves.Add((destinatario.Correo.ToUpperInvariant(), destinatario.TipoEnvio)))
            {
                throw new ValidacionNegocioNotificacionException(
                    "NOTIFICACION_DESTINATARIO_DUPLICADO", "La notificación contiene una dirección repetida para el mismo tipo de envío.");
            }
        }

        return copia.AsReadOnly();
    }

    internal static void ValidarFallo(string error, int maxIntentos, TimeSpan intervaloReintento)
    {
        if (maxIntentos < 1)
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_MAXIMO_INTENTOS_INVALIDO",
                "El máximo de intentos debe ser mayor o igual a uno.");
        }

        if (intervaloReintento <= TimeSpan.Zero)
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_INTERVALO_REINTENTO_INVALIDO",
                "El intervalo de reintento debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_MOTIVO_FALLO_REQUERIDO",
                "Debe indicarse el motivo del fallo.");
        }
    }
}
