namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

/// <summary>Representa una configuración que impide generar una notificación.</summary>
/// <remarks>
/// Usar como base de errores de reglas, destinatarios o plantillas mal configurados.
/// Una regla deshabilitada o un evento sin suscripción no constituyen por sí mismos
/// errores de configuración y deben tratarse como resultados normales del flujo.
/// </remarks>
public abstract class ConfiguracionNotificacionException : NotificacionesException
{
    /// <summary>Inicializa un error de configuración con su código y descripción.</summary>
    /// <param name="codigoError">Código estable del error de configuración.</param>
    /// <param name="mensaje">Descripción de la configuración que debe corregirse.</param>
    protected ConfiguracionNotificacionException(string codigoError, string mensaje)
        : base(codigoError, mensaje)
    {
    }
}
