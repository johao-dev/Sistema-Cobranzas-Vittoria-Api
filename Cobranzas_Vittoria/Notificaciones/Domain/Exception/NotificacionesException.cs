namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

/// <summary>
/// Representa un error del módulo de Notificaciones identificado por un código estable.
/// </summary>
/// <remarks>
/// Usar como base de las excepciones específicas del módulo para permitir su manejo común.
/// El mensaje debe describir el problema sin incluir direcciones de correo, credenciales
/// ni el contenido del mensaje. No envolver indiscriminadamente errores de persistencia.
/// </remarks>
public abstract class NotificacionesException : System.Exception
{
    /// <summary>Obtiene el código funcional que identifica el error independientemente del mensaje.</summary>
    public string CodigoError { get; }

    /// <summary>Inicializa un error del módulo y conserva su causa original, si existe.</summary>
    /// <param name="codigoError">Código estable utilizado para identificar el error.</param>
    /// <param name="mensaje">Descripción del problema sin datos sensibles.</param>
    /// <param name="innerException">Causa original del error, o null si no corresponde.</param>
    protected NotificacionesException(
        string codigoError,
        string mensaje,
        System.Exception? innerException = null)
        : base(mensaje, innerException)
    {
        CodigoError = codigoError;
    }
}
