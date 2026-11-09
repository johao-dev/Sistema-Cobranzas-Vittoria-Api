using Cobranzas_Vittoria.Notificaciones.Domain.Exception;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Validator;

/// <summary>Valida la asociación y el contenido de una plantilla antes de modificarla.</summary>
internal static class PlantillaCorreoValidator
{
    private const int LONGITUD_MAXIMA_ASUNTO = 250;

    internal static void ValidarEvento(int idCodigoEventoNotificacion)
    {
        if (idCodigoEventoNotificacion <= 0)
        {
            throw new ValidacionNegocioNotificacionException(
                "PLANTILLA_CORREO_EVENTO_INVALIDO", "El identificador del código de evento debe ser positivo.");
        }
    }

    internal static void ValidarContenido(string asunto, string cuerpoHtml)
    {
        if (string.IsNullOrWhiteSpace(asunto) || asunto.Length > LONGITUD_MAXIMA_ASUNTO)
        {
            throw new ValidacionNegocioNotificacionException(
                "PLANTILLA_CORREO_ASUNTO_INVALIDO",
                $"El asunto de la plantilla debe contener entre 1 y {LONGITUD_MAXIMA_ASUNTO} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(cuerpoHtml))
        {
            throw new ValidacionNegocioNotificacionException(
                "PLANTILLA_CORREO_CUERPO_REQUERIDO", "El cuerpo HTML de la plantilla es obligatorio.");
        }
    }
}
