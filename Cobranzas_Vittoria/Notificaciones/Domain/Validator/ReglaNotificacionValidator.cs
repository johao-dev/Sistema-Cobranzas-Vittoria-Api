using Cobranzas_Vittoria.Notificaciones.Domain.Exception;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Validator;

/// <summary>Valida la identificación y el nombre de las reglas de notificación.</summary>
internal static class ReglaNotificacionValidator
{
    private const int LONGITUD_MAXIMA_NOMBRE = 150;

    internal static void ValidarEvento(int idCodigoEventoNotificacion)
    {
        if (idCodigoEventoNotificacion <= 0)
        {
            throw new ValidacionNegocioNotificacionException(
                "REGLA_NOTIFICACION_EVENTO_INVALIDO", "El identificador del código de evento debe ser positivo.");
        }
    }

    internal static string ValidarYNormalizarNombre(string nombre)
    {
        string nombreNormalizado = nombre?.Trim() ?? string.Empty;

        if (nombreNormalizado.Length == 0 || nombreNormalizado.Length > LONGITUD_MAXIMA_NOMBRE)
        {
            throw new ValidacionNegocioNotificacionException(
                "REGLA_NOTIFICACION_NOMBRE_INVALIDO",
                $"El nombre de la regla debe contener entre 1 y {LONGITUD_MAXIMA_NOMBRE} caracteres.");
        }

        return nombreNormalizado;
    }
}
