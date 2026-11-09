using System.Net.Mail;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using static Cobranzas_Vittoria.Notificaciones.Domain.Model.NotificacionDestinatario;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Validator;

/// <summary>Valida los datos normalizados necesarios para crear un destinatario de notificación.</summary>
internal static class NotificacionDestinatarioValidator
{
    private const int LONGITUD_MAXIMA_CORREO = 254;
    private const int LONGITUD_MAXIMA_NOMBRE = 150;

    internal static void ValidarCreacion(string correo, string? nombre, TipoEnvioConfig tipoEnvio)
    {
        if (string.IsNullOrWhiteSpace(correo) || correo.Length > LONGITUD_MAXIMA_CORREO)
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_DESTINATARIO_CORREO_INVALIDO",
                $"El correo del destinatario debe contener entre 1 y {LONGITUD_MAXIMA_CORREO} caracteres.");
        }

        // MailAddress también admite nombres de presentación; aquí se requiere una dirección única y pura.
        if (correo.Contains('\r') || correo.Contains('\n')
            || !MailAddress.TryCreate(correo, out MailAddress? direccion)
            || !string.Equals(direccion.Address, correo, StringComparison.Ordinal))
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_DESTINATARIO_CORREO_INVALIDO",
                "El destinatario debe contener una única dirección de correo con formato válido.");
        }

        if (nombre is not null && nombre.Length > LONGITUD_MAXIMA_NOMBRE)
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_DESTINATARIO_NOMBRE_INVALIDO",
                $"El nombre del destinatario no debe superar los {LONGITUD_MAXIMA_NOMBRE} caracteres.");
        }

        if (!Enum.IsDefined(tipoEnvio))
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_DESTINATARIO_TIPO_ENVIO_INVALIDO",
                "El tipo de envío del destinatario debe ser TO, CC o BCC.");
        }
    }
}
