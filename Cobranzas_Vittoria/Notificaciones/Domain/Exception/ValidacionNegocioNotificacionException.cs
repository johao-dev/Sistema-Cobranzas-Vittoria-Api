namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

/// <summary>Representa el incumplimiento de una regla de negocio de Notificaciones.</summary>
/// <remarks>
/// Usar para proteger invariantes del módulo, como la presencia de destinatarios
/// o las transiciones permitidas de estado. No utilizar para fallos técnicos del
/// proveedor de correo ni para validar el formato de entradas HTTP.
/// </remarks>
public class ValidacionNegocioNotificacionException : NotificacionesException
{
    /// <summary>Inicializa una validación de negocio con su código y descripción.</summary>
    /// <param name="codigoError">Código estable de la regla incumplida.</param>
    /// <param name="mensaje">Descripción de la regla de negocio que no se cumple.</param>
    public ValidacionNegocioNotificacionException(string codigoError, string mensaje)
        : base(codigoError, mensaje)
    {
    }
}
