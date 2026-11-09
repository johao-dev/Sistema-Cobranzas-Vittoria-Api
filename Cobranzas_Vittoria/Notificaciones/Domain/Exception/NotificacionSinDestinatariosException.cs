namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

/// <summary>Indica que una notificación no dispone de destinatarios válidos.</summary>
/// <remarks>
/// Lanzar al intentar crear una notificación si, después de resolver y validar
/// los destinatarios configurados, no queda ninguno al que pueda enviarse el correo.
/// Una búsqueda individual sin resultados no implica este error si otras estrategias
/// aportan destinatarios válidos.
/// </remarks>
public sealed class NotificacionSinDestinatariosException : ValidacionNegocioNotificacionException
{
    /// <summary>Código estable para una notificación sin destinatarios válidos.</summary>
    public const string CODIGO = "NOTIFICACION_SIN_DESTINATARIOS";

    /// <summary>Inicializa el error para el evento que no pudo resolver destinatarios válidos.</summary>
    /// <param name="codigoEvento">Código funcional del evento que genera la notificación.</param>
    public NotificacionSinDestinatariosException(string codigoEvento)
        : base(CODIGO, $"La notificación del evento {codigoEvento} no dispone de destinatarios válidos.")
    {
    }

    /// <summary>Inicializa el error cuando el dominio dispone del ID del catálogo en lugar de su código funcional.</summary>
    /// <param name="idCodigoEventoNotificacion">Identificador del código de evento que genera la notificación.</param>
    public NotificacionSinDestinatariosException(int idCodigoEventoNotificacion)
        : base(CODIGO, $"La notificación del código de evento con ID {idCodigoEventoNotificacion} no dispone de destinatarios válidos.")
    {
    }
}
