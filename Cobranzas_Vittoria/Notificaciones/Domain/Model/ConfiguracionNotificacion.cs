namespace Cobranzas_Vittoria.Notificaciones.Domain.Model;

/// <summary>Lectura conjunta de un evento y su configuración, incluyendo componentes ausentes o desactivados.</summary>
/// <remarks>La aplicación distingue ausencia de suscripción, configuración incompleta y desactivación deliberada.</remarks>
/// <param name="IdCodigoEventoNotificacion">Identificador del evento en el catálogo.</param>
/// <param name="CodigoEvento">Código funcional del evento.</param>
/// <param name="Regla">Regla con sus destinatarios, o null si no existe.</param>
/// <param name="Plantilla">Plantilla del evento, o null si no existe.</param>
public sealed record ConfiguracionNotificacion(
    int IdCodigoEventoNotificacion,
    string CodigoEvento,
    ReglaNotificacion? Regla,
    PlantillaCorreo? Plantilla);
