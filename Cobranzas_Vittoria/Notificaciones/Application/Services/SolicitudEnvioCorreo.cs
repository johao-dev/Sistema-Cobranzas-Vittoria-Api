namespace Cobranzas_Vittoria.Notificaciones.Application.Services;

/// <summary>Mensaje compuesto exclusivamente con el contenido y los destinatarios previamente almacenados.</summary>
public sealed record SolicitudEnvioCorreo(
    string Asunto,
    string CuerpoHtml,
    IReadOnlyCollection<DireccionCorreo> To,
    IReadOnlyCollection<DireccionCorreo> Cc,
    IReadOnlyCollection<DireccionCorreo> Bcc);
