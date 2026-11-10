namespace Cobranzas_Vittoria.Notificaciones.Domain.Exception;

public sealed class PlantillaCorreoInvalidaException(string motivo)
    : ConfiguracionNotificacionException("NOTIFICACION_PLANTILLA_INVALIDA", motivo);
