namespace Cobranzas_Vittoria.Notificaciones.Application.Services;

/// <summary>Transporte de correo independiente de SMTP y sus bibliotecas.</summary>
public interface IEmailSender
{
    /// <summary>Finaliza satisfactoriamente únicamente cuando el proveedor confirma la aceptación del mensaje.</summary>
    /// <remarks>
    /// La aceptación no acredita la entrega al buzón. El adaptador traduce solo fallos de transporte a
    /// EnvioCorreoException (EsTransitorio: true, false o null); los demás errores se propagan sin envolver.
    /// Debe respetar la cancelación y no incluir credenciales, destinatarios ni contenido en el mensaje
    /// de la excepción. La cuenta remitente y su autenticación pertenecen a Infrastructure.
    /// </remarks>
    Task EnviarAsync(SolicitudEnvioCorreo solicitud, CancellationToken cancellationToken = default);
}
