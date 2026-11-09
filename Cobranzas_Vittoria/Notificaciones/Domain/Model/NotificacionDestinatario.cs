using Cobranzas_Vittoria.Notificaciones.Domain.Validator;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Model;

/// <summary>Conserva los datos concretos de un destinatario al generar una notificación.</summary>
public class NotificacionDestinatario
{
    public long IdNotificacionDestinatario { get; private set; }
    public long IdNotificacion { get; private set; }

    public string Correo { get; private set; } = string.Empty;
    public string? Nombre { get; private set; }
    public TipoEnvioConfig TipoEnvio { get; private set; }

    public enum TipoEnvioConfig
    {
        To,
        Cc,
        Bcc
    }

    private NotificacionDestinatario() { }

    /// <summary>Crea un destinatario normalizando sus textos y validando sus datos de envío.</summary>
    /// <param name="correo">Dirección de correo requerida, de hasta 254 caracteres una vez recortada.</param>
    /// <param name="nombre">Nombre opcional, de hasta 150 caracteres una vez recortado.</param>
    /// <param name="tipoEnvio">Participación del destinatario como principal, copia o copia oculta.</param>
    /// <returns>Un destinatario nuevo con sus identificadores pendientes de persistencia.</returns>
    /// <exception cref="Exception.ValidacionNegocioNotificacionException">
    /// Los datos del destinatario incumplen las reglas de creación.
    /// </exception>
    public static NotificacionDestinatario Crear(
        string correo,
        string? nombre,
        TipoEnvioConfig tipoEnvio)
    {
        string correoNormalizado = correo?.Trim() ?? string.Empty;
        string? nombreNormalizado = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();

        NotificacionDestinatarioValidator.ValidarCreacion(correoNormalizado, nombreNormalizado, tipoEnvio);

        return new NotificacionDestinatario
        {
            Correo = correoNormalizado,
            Nombre = nombreNormalizado,
            TipoEnvio = tipoEnvio
        };
    }

    /// <summary>Restaura datos persistidos sin normalizarlos ni ejecutar las reglas de una alta nueva.</summary>
    /// <param name="idNotificacionDestinatario">Identificador persistido del destinatario.</param>
    /// <param name="idNotificacion">Identificador de la notificación a la que pertenece.</param>
    /// <param name="correo">Dirección conservada al generar la notificación.</param>
    /// <param name="nombre">Nombre conservado al generar la notificación.</param>
    /// <param name="tipoEnvio">Tipo de envío persistido.</param>
    /// <returns>El destinatario con sus identidades y datos originales.</returns>
    public static NotificacionDestinatario Reconstruir(
        long idNotificacionDestinatario,
        long idNotificacion,
        string correo,
        string? nombre,
        TipoEnvioConfig tipoEnvio)
    {
        return new NotificacionDestinatario
        {
            IdNotificacionDestinatario = idNotificacionDestinatario,
            IdNotificacion = idNotificacion,
            Correo = correo,
            Nombre = nombre,
            TipoEnvio = tipoEnvio
        };
    }
}
