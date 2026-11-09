namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

/// <summary>Indica que una notificación solicitada explícitamente por identificador no existe.</summary>
/// <remarks>
/// Lanzar cuando un caso de uso necesita una notificación concreta y no la encuentra.
/// No usar cuando la consulta periódica de la cola no devuelve pendientes;
/// una cola vacía es un resultado normal.
/// </remarks>
public sealed class NotificacionNoEncontradaException : NotificacionesException
{
    /// <summary>Código estable para una notificación inexistente.</summary>
    public const string CODIGO = "NOTIFICACION_NO_ENCONTRADA";

    /// <summary>Inicializa el error para el identificador solicitado.</summary>
    /// <param name="idNotificacion">Identificador de la notificación que no se encontró.</param>
    public NotificacionNoEncontradaException(long idNotificacion)
        : base(CODIGO, $"La notificación {idNotificacion} no existe.")
    {
    }
}
