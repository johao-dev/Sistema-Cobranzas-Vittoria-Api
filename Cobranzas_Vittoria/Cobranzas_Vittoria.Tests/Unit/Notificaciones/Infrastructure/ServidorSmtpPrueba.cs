using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Infrastructure;

/// <summary>Servidor de un solo cliente, TLS real y credenciales ficticias, siempre en loopback.</summary>
internal sealed class ServidorSmtpPrueba : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _detener = new();
    private readonly Task _servidor;
    internal X509Certificate2 Certificado { get; }
    internal int Puerto => ((IPEndPoint)_listener.LocalEndpoint).Port;
    internal int CodigoAuth { get; init; } = 235;
    internal int CodigoDestinatario { get; init; } = 250;
    internal int RechazarDesdeDestinatario { get; init; } = 1;
    internal int CodigoMensaje { get; init; } = 250;
    internal bool ResultadoIncierto { get; init; }
    internal bool EsperarDatos { get; init; }
    internal bool FallarCierre { get; init; }
    internal bool StartTls { get; init; }
    internal bool Pipelining { get; init; }
    internal List<string> Destinatarios { get; } = new();
    internal string? Contenido { get; private set; }
    internal TaskCompletionSource DatosRecibidos { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal ServidorSmtpPrueba()
    {
        using RSA clave = RSA.Create(2048);
        CertificateRequest solicitud = new("CN=localhost", clave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        SubjectAlternativeNameBuilder nombres = new();
        nombres.AddDnsName("localhost");
        solicitud.CertificateExtensions.Add(nombres.Build());
        solicitud.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        solicitud.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using X509Certificate2 generado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Certificado = new X509Certificate2(generado.Export(X509ContentType.Pfx));
        _listener.Start();
        _servidor = Task.Run(EjecutarAsync);
    }

    private async Task EjecutarAsync()
    {
        try
        {
            using TcpClient cliente = await _listener.AcceptTcpClientAsync(_detener.Token);
            Stream canal = cliente.GetStream();
            SslStream? tls = null;
            if (!StartTls)
            {
                tls = new SslStream(canal, leaveInnerStreamOpen: true);
                await AutenticarTls(tls);
                canal = tls;
            }

            StreamReader lector = CrearLector(canal);
            StreamWriter escritor = CrearEscritor(canal);
            await escritor.WriteLineAsync("220 localhost SMTP prueba");

            while (!_detener.IsCancellationRequested)
            {
                string? linea = await lector.ReadLineAsync(_detener.Token);
                if (linea is null) break;
                if (linea.StartsWith("EHLO", StringComparison.Ordinal))
                {
                    await escritor.WriteLineAsync("250-localhost");
                    if (Pipelining) await escritor.WriteLineAsync("250-PIPELINING");
                    if (StartTls && tls is null) await escritor.WriteLineAsync("250-STARTTLS");
                    await escritor.WriteLineAsync("250 AUTH PLAIN");
                }
                else if (linea == "STARTTLS")
                {
                    await escritor.WriteLineAsync("220 Iniciar TLS");
                    tls = new SslStream(canal, leaveInnerStreamOpen: true);
                    await AutenticarTls(tls);
                    canal = tls;
                    lector.Dispose();
                    escritor.Dispose();
                    lector = CrearLector(canal);
                    escritor = CrearEscritor(canal);
                }
                else if (linea.StartsWith("AUTH", StringComparison.Ordinal))
                {
                    await escritor.WriteLineAsync($"{CodigoAuth} Respuesta autenticacion prueba");
                }
                else if (linea.StartsWith("MAIL FROM", StringComparison.Ordinal))
                {
                    await escritor.WriteLineAsync("250 Remitente aceptado");
                }
                else if (linea.StartsWith("RCPT TO", StringComparison.Ordinal))
                {
                    Destinatarios.Add(linea);
                    int codigo = Destinatarios.Count >= RechazarDesdeDestinatario ? CodigoDestinatario : 250;
                    await escritor.WriteLineAsync($"{codigo} Respuesta destinatario prueba");
                }
                else if (linea == "DATA")
                {
                    await escritor.WriteLineAsync("354 Enviar mensaje");
                    StringBuilder datos = new();
                    while ((linea = await lector.ReadLineAsync(_detener.Token)) is not null && linea != ".")
                    {
                        datos.AppendLine(linea.StartsWith("..", StringComparison.Ordinal) ? linea[1..] : linea);
                    }
                    Contenido = datos.ToString();
                    DatosRecibidos.TrySetResult();
                    if (ResultadoIncierto) break;
                    if (EsperarDatos) await Task.Delay(Timeout.Infinite, _detener.Token);
                    await escritor.WriteLineAsync($"{CodigoMensaje} Respuesta mensaje prueba");
                }
                else if (linea == "QUIT")
                {
                    if (!FallarCierre) await escritor.WriteLineAsync("221 Cerrando");
                    break;
                }
                else if (linea == "RSET")
                {
                    await escritor.WriteLineAsync("250 Reiniciado");
                }
                else
                {
                    throw new InvalidOperationException("Comando SMTP de prueba inesperado.");
                }
            }

            lector.Dispose();
            escritor.Dispose();
            tls?.Dispose();
        }
        catch (OperationCanceledException) when (_detener.IsCancellationRequested) { }
        catch (IOException) { /* Desconexión intencional del cliente en pruebas de fallo/cancelación. */ }
        catch (AuthenticationException) { /* Certificado rechazado intencionalmente por el cliente. */ }
    }

    private Task AutenticarTls(SslStream tls) => tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
    {
        ServerCertificate = Certificado,
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
    }, _detener.Token);

    private static StreamReader CrearLector(Stream canal) => new(canal, Encoding.UTF8, false, 1024, leaveOpen: true);
    private static StreamWriter CrearEscritor(Stream canal) => new(canal, new UTF8Encoding(false), 1024, leaveOpen: true)
    { NewLine = "\r\n", AutoFlush = true };

    public async ValueTask DisposeAsync()
    {
        await _detener.CancelAsync();
        _listener.Stop();
        await _servidor.WaitAsync(TimeSpan.FromSeconds(5));
        Certificado.Dispose();
        _detener.Dispose();
    }
}
