using System.Net.Sockets;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.Email;

/// <summary>Un intento SMTP acotado; no reintenta ni acepta certificados sin verificar.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly Func<SmtpClient> _crearCliente;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
        : this(options, logger, () => new SmtpClient()) { }

    // Permite usar un almacén de confianza aislado en las pruebas TLS con un servidor local.
    internal SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger, Func<SmtpClient> crearCliente)
    {
        _options = options.Value;
        _logger = logger;
        _crearCliente = crearCliente;
    }

    public async Task EnviarAsync(SolicitudEnvioCorreo solicitud, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _options.Validar();

        using MimeMessage mensaje = new();
        MailboxAddress remitente = new(_options.NombreVisible, _options.Remitente);
        mensaje.From.Add(remitente);
        mensaje.Subject = solicitud.Asunto;
        mensaje.Body = new TextPart("html") { Text = solicitud.CuerpoHtml };
        mensaje.To.AddRange(solicitud.To.Select(Direccion));
        mensaje.Cc.AddRange(solicitud.Cc.Select(Direccion));

        // BCC participa únicamente en el sobre SMTP; no se crea un encabezado Bcc visible.
        List<MailboxAddress> destinatarios = solicitud.To.Concat(solicitud.Cc).Concat(solicitud.Bcc)
            .Select(Direccion).DistinctBy(d => d.Address, StringComparer.OrdinalIgnoreCase).ToList();

        if (destinatarios.Count == 0 || solicitud.Asunto.Contains('\r') || solicitud.Asunto.Contains('\n'))
        {
            throw new ArgumentException("El mensaje necesita destinatarios y un asunto sin saltos de línea.", nameof(solicitud));
        }

        using SmtpClient cliente = _crearCliente();
        cliente.Timeout = checked(_options.TimeoutOperacionSegundos * 1000);
        using CancellationTokenSource limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutTotalSegundos));

        FaseSmtp fase = FaseSmtp.Conexion;
        bool aceptado = false;

        try
        {
            SecureSocketOptions tls = _options.ModoTls == ModoTlsSmtp.StartTls
                ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
            await cliente.ConnectAsync(_options.Host, _options.Puerto, tls, limite.Token);

            fase = FaseSmtp.Autenticacion;
            await cliente.AuthenticateAsync(_options.Usuario, _options.Contrasena, limite.Token);

            fase = FaseSmtp.Envio;
            // MailKit aborta ante cualquier RCPT rechazado antes de transmitir el contenido.
            // Así no se reenvía a destinatarios ya aceptados parcialmente por esta implementación.
            await cliente.SendAsync(mensaje, remitente, destinatarios, limite.Token);
            aceptado = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (System.Exception error) when (EsErrorTransporte(error))
        {
            throw Clasificar(error, fase);
        }
        finally
        {
            // Un fallo de QUIT nunca transforma una aceptación confirmada en un nuevo intento.
            if (cliente.IsConnected)
            {
                using CancellationTokenSource cierre = new(TimeSpan.FromSeconds(_options.TimeoutCierreSegundos));
                try
                {
                    await cliente.DisconnectAsync(aceptado, cierre.Token);
                }
                catch (System.Exception error) when (EsErrorTransporte(error))
                {
                    _logger.LogWarning("Fallo al cerrar SMTP; aceptación confirmada {Aceptado}, tipo {TipoError}",
                        aceptado, error.GetType().Name);
                }
            }
        }
    }

    private static MailboxAddress Direccion(DireccionCorreo direccion) => new(direccion.Nombre ?? "", direccion.Correo);

    private static bool EsErrorTransporte(System.Exception error) => error is IOException or SocketException
        or SmtpCommandException or SmtpProtocolException or MailKit.Security.AuthenticationException or SslHandshakeException
        or System.Security.Authentication.AuthenticationException or ServiceNotAuthenticatedException
        or ServiceNotConnectedException or OperationCanceledException or NotSupportedException;

    private static EnvioCorreoException Clasificar(System.Exception error, FaseSmtp fase)
    {
        bool operativo = error is MailKit.Security.AuthenticationException
            or System.Security.Authentication.AuthenticationException or SslHandshakeException
            or ServiceNotAuthenticatedException or NotSupportedException;
        bool? transitorio;

        SmtpCommandException? comando = error as SmtpCommandException
            ?? (error is MailKit.Security.AuthenticationException ? error.InnerException as SmtpCommandException : null);

        if (comando is not null)
        {
            int codigo = (int)comando.StatusCode;
            transitorio = codigo is >= 400 and < 500 ? true : codigo is >= 500 and < 600 ? false : null;
            if (fase == FaseSmtp.Autenticacion && transitorio == true)
            {
                operativo = false;
            }
            operativo |= (fase != FaseSmtp.Envio && transitorio == false)
                || comando.ErrorCode == SmtpErrorCode.SenderNotAccepted || codigo is 530 or 534 or 535 or 538;
        }
        else
        {
            // Durante DATA una desconexión/timeout puede ocurrir después de la aceptación del servidor.
            transitorio = operativo ? false : fase == FaseSmtp.Envio ? null : true;
        }

        return new EnvioCorreoException(operativo ? "SMTP requiere intervención operativa."
            : "El transporte SMTP no confirmó el envío.", transitorio, error, operativo);
    }

    private enum FaseSmtp { Conexion, Autenticacion, Envio }
}
