using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Email;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using NUnit.Framework;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Infrastructure;

public class SmtpEmailSenderTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Aceptacion_ToCcBccSeEnviaSinEncabezadoBcc(bool startTls)
    {
        await using ServidorSmtpPrueba servidor = new() { StartTls = startTls };
        await Sender(servidor).EnviarAsync(Solicitud());
        using MemoryStream contenido = new(System.Text.Encoding.UTF8.GetBytes(servidor.Contenido!));
        using MimeMessage mensaje = MimeMessage.Load(contenido);

        Assert.Multiple(() =>
        {
            Assert.That(servidor.Destinatarios.Count, Is.EqualTo(3));
            Assert.That(servidor.Destinatarios.Any(d => d.Contains("bcc@example.com", StringComparison.Ordinal)), Is.True);
            Assert.That(mensaje.To.Mailboxes.Single().Address, Is.EqualTo("to@example.com"));
            Assert.That(mensaje.Cc.Mailboxes.Single().Address, Is.EqualTo("cc@example.com"));
            Assert.That(mensaje.Bcc, Is.Empty);
            Assert.That(mensaje.Headers.Contains(HeaderId.Bcc), Is.False);
            Assert.That(mensaje.Subject, Is.EqualTo("Prueba SMTP"));
            Assert.That(mensaje.HtmlBody, Does.Contain("Prueba aislada"));
            Assert.That(mensaje.From.Mailboxes.Single().Address, Is.EqualTo("notificaciones@vittoriainmobiliaria.com"));
        });
    }

    [TestCase(450, true)]
    [TestCase(550, false)]
    public async Task RechazoDestinatario_SeClasificaPorCodigoSinTransmitirDatos(int codigo, bool transitorio)
    {
        await using ServidorSmtpPrueba servidor = new() { CodigoDestinatario = codigo };
        EnvioCorreoException? error = Assert.ThrowsAsync<EnvioCorreoException>(async () => await Sender(servidor).EnviarAsync(Solicitud()));
        Assert.That(error!.EsTransitorio, Is.EqualTo(transitorio));
        Assert.That(error.RequiereIntervencion, Is.False);
        Assert.That(servidor.Contenido, Is.Null);
        Assert.That(error.Message, Does.Not.Contain("example.com"));
    }

    [TestCase(451, true)]
    [TestCase(554, false)]
    public async Task RechazoMensaje_SeClasificaPorCodigo(int codigo, bool transitorio)
    {
        await using ServidorSmtpPrueba servidor = new() { CodigoMensaje = codigo };
        EnvioCorreoException? error = Assert.ThrowsAsync<EnvioCorreoException>(async () => await Sender(servidor).EnviarAsync(Solicitud()));
        Assert.That(error!.EsTransitorio, Is.EqualTo(transitorio));
        Assert.That(error.RequiereIntervencion, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AceptacionParcialRcpt_AbortaAntesDeData(bool pipelining)
    {
        await using ServidorSmtpPrueba servidor = new()
        { CodigoDestinatario = 550, RechazarDesdeDestinatario = 2, Pipelining = pipelining };
        Assert.ThrowsAsync<EnvioCorreoException>(async () => await Sender(servidor).EnviarAsync(Solicitud()));
        Assert.That(servidor.Destinatarios.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(servidor.Contenido, Is.Null);
    }

    [Test]
    public async Task DesconexionTrasData_ResultadoEsIncierto()
    {
        await using ServidorSmtpPrueba servidor = new() { ResultadoIncierto = true };
        EnvioCorreoException? error = Assert.ThrowsAsync<EnvioCorreoException>(async () => await Sender(servidor).EnviarAsync(Solicitud()));
        Assert.That(error!.EsTransitorio, Is.Null);
    }

    [Test]
    public async Task TimeoutTotalDuranteData_ResultadoEsIncierto()
    {
        await using ServidorSmtpPrueba servidor = new() { EsperarDatos = true };
        EnvioCorreoException? error = Assert.ThrowsAsync<EnvioCorreoException>(async () => await Sender(servidor, timeout: 1).EnviarAsync(Solicitud()));
        Assert.That(error!.EsTransitorio, Is.Null);
    }

    [Test]
    public async Task CancelacionExterna_SePropaga()
    {
        await using ServidorSmtpPrueba servidor = new() { EsperarDatos = true };
        using CancellationTokenSource source = new();
        Task envio = Sender(servidor).EnviarAsync(Solicitud(), source.Token);
        await servidor.DatosRecibidos.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await source.CancelAsync();
        Assert.That(async () => await envio, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task FalloQuitDespuesDeAceptacion_NoProvocaReenvio()
    {
        await using ServidorSmtpPrueba servidor = new() { FallarCierre = true };
        Assert.DoesNotThrowAsync(async () => await Sender(servidor).EnviarAsync(Solicitud()));
        Assert.That(servidor.Contenido, Is.Not.Null);
    }

    [TestCase(535, false, true)]
    [TestCase(454, true, false)]
    public async Task Autenticacion_DistingueCredencialesDeErrorTemporal(int codigo, bool transitorio, bool operativo)
    {
        await using ServidorSmtpPrueba servidor = new() { CodigoAuth = codigo };
        EnvioCorreoException? error = Assert.ThrowsAsync<EnvioCorreoException>(async () => await Sender(servidor).EnviarAsync(Solicitud()));
        Assert.That(error!.EsTransitorio, Is.EqualTo(transitorio));
        Assert.That(error.RequiereIntervencion, Is.EqualTo(operativo));
        Assert.That(servidor.Contenido, Is.Null);
    }

    [Test]
    public async Task CertificadoNoConfiable_SeRechazaComoFalloOperativo()
    {
        await using ServidorSmtpPrueba servidor = new();
        SmtpEmailSender sender = new(Options.Create(CrearOpciones(servidor)), NullLogger<SmtpEmailSender>.Instance);
        EnvioCorreoException? error = Assert.ThrowsAsync<EnvioCorreoException>(async () => await sender.EnviarAsync(Solicitud()));
        Assert.That(error!.RequiereIntervencion, Is.True);
        Assert.That(servidor.Contenido, Is.Null);
    }

    private static SmtpEmailSender Sender(ServidorSmtpPrueba servidor, int timeout = 5)
    {
        SmtpOptions options = CrearOpciones(servidor);
        options.TimeoutTotalSegundos = timeout;
        return new SmtpEmailSender(Microsoft.Extensions.Options.Options.Create(options), NullLogger<SmtpEmailSender>.Instance,
            () => new SmtpClient
            {
                // Confianza privada limitada al certificado del servidor local y su nombre.
                ServerCertificateValidationCallback = (_, certificado, _, errores) =>
                {
                    using X509Chain cadena = new();
                    cadena.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    cadena.ChainPolicy.CustomTrustStore.Add(servidor.Certificado);
                    cadena.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    return (errores & SslPolicyErrors.RemoteCertificateNameMismatch) == 0
                        && certificado is X509Certificate2 cert && cadena.Build(cert);
                }
            });
    }

    private static SmtpOptions CrearOpciones(ServidorSmtpPrueba servidor) => new()
    {
        Host = "localhost", Puerto = servidor.Puerto, Usuario = "usuario-prueba", Contrasena = "clave-ficticia",
        ModoTls = servidor.StartTls ? ModoTlsSmtp.StartTls : ModoTlsSmtp.SslOnConnect, TimeoutOperacionSegundos = 3
    };

    private static SolicitudEnvioCorreo Solicitud() => new("Prueba SMTP", "<p>Prueba aislada</p>",
        [new DireccionCorreo("to@example.com")], [new DireccionCorreo("cc@example.com")], [new DireccionCorreo("bcc@example.com")]);
}
