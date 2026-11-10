namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

/// <summary>Representa un fallo del transporte de correo independiente del proveedor utilizado.</summary>
/// <remarks>
/// Lanzar desde el adaptador de correo cuando falla un intento de envío, conservando
/// la causa original para diagnóstico. El consumidor decide los reintentos según
/// la clasificación del fallo y su política; agotar intentos no requiere otra excepción.
/// La causa original puede contener datos sensibles y no debe exponerse en respuestas HTTP.
/// </remarks>
public sealed class EnvioCorreoException : NotificacionesException
{
    /// <summary>Código estable para un fallo del transporte de correo.</summary>
    public const string CODIGO = "NOTIFICACION_ENVIO_CORREO_FALLIDO";

    /// <summary>
    /// Obtiene true para un fallo transitorio, false para uno permanente o null si se desconoce.
    /// </summary>
    /// <remarks>Un fallo desconocido no debe clasificarse automáticamente como permanente.</remarks>
    public bool? EsTransitorio { get; }

    /// <summary>El transporte requiere corregir configuración y reanudar el proceso antes de otro envío.</summary>
    public bool RequiereIntervencion { get; }

    /// <summary>Inicializa el fallo de envío con su clasificación y causa original.</summary>
    /// <param name="mensaje">Descripción del fallo sin credenciales, correos ni contenido del mensaje.</param>
    /// <param name="esTransitorio">True si es transitorio, false si es permanente o null si se desconoce.</param>
    /// <param name="innerException">Excepción original del transporte, si está disponible.</param>
    /// <param name="requiereIntervencion">True si debe suspenderse el procesamiento hasta corregir el transporte.</param>
    public EnvioCorreoException(
        string mensaje,
        bool? esTransitorio = null,
        System.Exception? innerException = null,
        bool requiereIntervencion = false)
        : base(CODIGO, mensaje, innerException)
    {
        EsTransitorio = esTransitorio;
        RequiereIntervencion = requiereIntervencion;
    }
}
