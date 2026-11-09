using Cobranzas_Vittoria.Notificaciones.Domain.Exception;

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
