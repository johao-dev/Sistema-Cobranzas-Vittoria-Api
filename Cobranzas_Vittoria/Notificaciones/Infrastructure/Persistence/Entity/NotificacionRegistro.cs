using Cobranzas_Vittoria.Notificaciones.Domain.Model;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.Persistence.Entity;

/// <summary>Traduce textos SQL y datetime2 sin zona a la instantánea de dominio UTC.</summary>
internal sealed class NotificacionRegistro
{
    public long IdNotificacion { get; set; }
    public Guid IdEvento { get; set; }
    public int IdCodigoEventoNotificacion { get; set; }
    public int IdReferencia { get; set; }
    public string AsuntoGenerado { get; set; } = "";
    public string CuerpoHtmlGenerado { get; set; } = "";
    public string Estado { get; set; } = "";
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaInicioProcesamiento { get; set; }
    public DateTime? FechaProximoIntento { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }

    internal Notificacion Reconstruir(IEnumerable<DestinatarioRegistro> destinatarios)
    {
        Notificacion.EstadoNotificacion estado = Estado switch
        {
            "PENDIENTE" => Notificacion.EstadoNotificacion.Pendiente,
            "PROCESANDO" => Notificacion.EstadoNotificacion.Procesando,
            "ENVIADA" => Notificacion.EstadoNotificacion.Enviada,
            "ERROR" => Notificacion.EstadoNotificacion.Error,
            _ => throw new InvalidOperationException("Estado SQL de notificación no soportado.")
        };

        return Notificacion.Reconstruir(IdNotificacion, IdEvento, IdCodigoEventoNotificacion, IdReferencia,
            AsuntoGenerado, CuerpoHtmlGenerado, estado, Utc(FechaCreacion)!.Value,
            Utc(FechaInicioProcesamiento), Utc(FechaProximoIntento), Utc(FechaEnvio), Intentos, UltimoError,
            destinatarios.Select(d => d.Reconstruir()));
    }

    private static DateTime? Utc(DateTime? fecha) => fecha.HasValue
        ? DateTime.SpecifyKind(fecha.Value, DateTimeKind.Utc) : null;
}

internal sealed class DestinatarioRegistro
{
    public long IdNotificacionDestinatario { get; set; }
    public long IdNotificacion { get; set; }
    public string Correo { get; set; } = "";
    public string? Nombre { get; set; }
    public string TipoEnvio { get; set; } = "";

    internal NotificacionDestinatario Reconstruir() => NotificacionDestinatario.Reconstruir(
        IdNotificacionDestinatario, IdNotificacion, Correo, Nombre, ConversionSql.LeerEnvio(TipoEnvio));
}

internal static class ConversionSql
{
    internal static NotificacionDestinatario.TipoEnvioConfig LeerEnvio(string tipo) => tipo switch
    {
        "TO" => NotificacionDestinatario.TipoEnvioConfig.To,
        "CC" => NotificacionDestinatario.TipoEnvioConfig.Cc,
        "BCC" => NotificacionDestinatario.TipoEnvioConfig.Bcc,
        _ => throw new InvalidOperationException("Tipo de envío SQL no soportado.")
    };

    internal static string EscribirEnvio(NotificacionDestinatario.TipoEnvioConfig tipo) => tipo switch
    {
        NotificacionDestinatario.TipoEnvioConfig.To => "TO",
        NotificacionDestinatario.TipoEnvioConfig.Cc => "CC",
        NotificacionDestinatario.TipoEnvioConfig.Bcc => "BCC",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };
}
