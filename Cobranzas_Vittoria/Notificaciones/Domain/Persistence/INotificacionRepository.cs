using Cobranzas_Vittoria.Notificaciones.Domain.Model;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Persistence;

/// <summary>Persiste instantáneas y coordina de forma atómica los intentos de procesamiento.</summary>
public interface INotificacionRepository
{
    /// <summary>Registra una notificación nueva y todos sus destinatarios en una sola transacción.</summary>
    /// <remarks>
    /// La unicidad SQL de IdEvento resuelve también carreras concurrentes. Si ya existe, devuelve su ID
    /// sin modificar contenido ni destinatarios; la entrega duplicada no constituye un error de negocio.
    /// </remarks>
    Task<ResultadoRegistroNotificacion> RegistrarSiNoExisteAsync(
        Notificacion notificacion,
        CancellationToken cancellationToken = default);

    /// <summary>Reconstruye la notificación y sus destinatarios originales; devuelve null si no existe.</summary>
    Task<Notificacion?> ObtenerPorIdAsync(long idNotificacion, CancellationToken cancellationToken = default);

    /// <summary>Reclama hasta la cantidad indicada de pendientes cuya fecha de reintento ya llegó.</summary>
    /// <remarks>
    /// La selección y el cambio a PROCESANDO son atómicos: se incrementan Intentos, se fija el inicio en
    /// ahoraUtc y se limpia la fecha de próximo intento. Se devuelven los destinatarios originales.
    /// El worker no vuelve a invocar IniciarProcesamiento ni mantiene una transacción abierta durante el envío.
    /// Una cola vacía devuelve una colección vacía. Solo se admiten cantidades positivas y fechas UTC.
    /// </remarks>
    Task<IReadOnlyCollection<Notificacion>> ReclamarPendientesAsync(
        int cantidad,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Guarda el resultado de un intento sin modificar el contenido ni los destinatarios originales.</summary>
    /// <remarks>
    /// Actualiza solo si la fila sigue en PROCESANDO con Intentos igual a intentoEsperado y la entidad
    /// corresponde al mismo intento. Los estados de resultado admitidos son PENDIENTE, ENVIADA y ERROR.
    /// Devuelve false si otro procesamiento ganó la concurrencia; nunca sobrescribe un intento posterior.
    /// No se persiste el contador de intentos desde el resultado: se incrementa al reclamar el trabajo.
    /// </remarks>
    Task<bool> GuardarResultadoAsync(
        Notificacion notificacion,
        int intentoEsperado,
        CancellationToken cancellationToken = default);
}
