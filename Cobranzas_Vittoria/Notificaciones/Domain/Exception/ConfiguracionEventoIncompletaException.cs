namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

/// <summary>Indica que falta un componente obligatorio de la configuración de un evento.</summary>
/// <remarks>
/// Lanzar cuando un evento que debe generar notificaciones carece de su regla,
/// plantilla u otro componente obligatorio. No usar para eventos sin suscripción
/// ni para componentes deshabilitados deliberadamente.
/// </remarks>
public sealed class ConfiguracionEventoIncompletaException : ConfiguracionNotificacionException
{
    /// <summary>Código estable para una configuración de evento incompleta.</summary>
    public const string CODIGO = "NOTIFICACION_CONFIGURACION_EVENTO_INCOMPLETA";

    /// <summary>Inicializa el error indicando el evento y el componente ausente.</summary>
    /// <param name="codigoEvento">Código funcional del evento afectado.</param>
    /// <param name="componenteFaltante">Nombre del componente requerido, por ejemplo, regla o plantilla.</param>
    public ConfiguracionEventoIncompletaException(string codigoEvento, string componenteFaltante)
        : base(CODIGO, $"La configuración del evento {codigoEvento} está incompleta: falta {componenteFaltante}.")
    {
    }
}
