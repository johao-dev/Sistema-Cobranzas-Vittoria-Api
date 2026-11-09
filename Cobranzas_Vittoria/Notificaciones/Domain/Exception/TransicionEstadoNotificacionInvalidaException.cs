using static Cobranzas_Vittoria.Notificaciones.Domain.Model.Notificacion;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

/// <summary>Indica que el estado actual de una notificación impide una operación de su ciclo de vida.</summary>
/// <remarks>
/// Lanzar desde el modelo cuando se solicita una transición no permitida, como
/// marcar enviada una notificación que no está en procesamiento. No usar para
/// señalar que otro worker ya reclamó una fila: esa concurrencia es un resultado
/// normal de la operación de persistencia.
/// </remarks>
public sealed class TransicionEstadoNotificacionInvalidaException : ValidacionNegocioNotificacionException
{
    /// <summary>Código estable para una transición de estado no permitida.</summary>
    public const string CODIGO = "NOTIFICACION_TRANSICION_ESTADO_INVALIDA";

    /// <summary>Inicializa el error indicando el estado actual y la operación solicitada.</summary>
    /// <param name="estadoActual">Estado de la notificación antes de intentar la operación.</param>
    /// <param name="operacion">Nombre de la operación que el estado actual no permite.</param>
    public TransicionEstadoNotificacionInvalidaException(EstadoNotificacion estadoActual, string operacion)
        : base(CODIGO, $"No se puede ejecutar la operación {operacion} con la notificación en estado {estadoActual}.")
    {
    }
}
