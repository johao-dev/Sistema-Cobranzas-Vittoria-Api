using Cobranzas_Vittoria.Notificaciones.Domain.Model;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Persistence;

/// <summary>Consulta y persiste la configuración; no resuelve usuarios ni renderiza plantillas.</summary>
public interface IConfiguracionNotificacionRepository
{
    /// <summary>Obtiene la configuración sin filtrar componentes desactivados; devuelve null si el evento no existe.</summary>
    /// <remarks>
    /// Incluye los destinatarios de la regla. Las relaciones se leen de forma consistente, sin mezclar
    /// versiones de una actualización concurrente. La aplicación decide si omite o rechaza la generación.
    /// </remarks>
    Task<ConfiguracionNotificacion?> ObtenerPorCodigoEventoAsync(
        string codigoEvento,
        CancellationToken cancellationToken = default);

    /// <summary>Inserta o actualiza la regla y sus destinatarios en una transacción, conservando la unicidad por evento.</summary>
    /// <remarks>No modifica notificaciones generadas ni reasigna la pertenencia de instrucciones de otras reglas.</remarks>
    Task GuardarReglaAsync(ReglaNotificacion regla, CancellationToken cancellationToken = default);

    /// <summary>Inserta o actualiza la plantilla, conservando la unicidad por evento y los correos ya generados.</summary>
    Task GuardarPlantillaAsync(PlantillaCorreo plantilla, CancellationToken cancellationToken = default);
}
