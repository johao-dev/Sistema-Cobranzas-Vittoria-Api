using System.ComponentModel.DataAnnotations;
using MimeKit;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.Email;

public enum ModoTlsSmtp { StartTls, SslOnConnect }

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Puerto { get; set; } = 587;
    public ModoTlsSmtp ModoTls { get; set; } = ModoTlsSmtp.StartTls;
    public string Usuario { get; set; } = "";
    public string Contrasena { get; set; } = "";
    public string Remitente { get; set; } = "notificaciones@vittoriainmobiliaria.com";
    public string NombreVisible { get; set; } = "Notificaciones Vittoria";
    [Range(1, 300)]
    public int TimeoutOperacionSegundos { get; set; } = 30;
    [Range(1, 300)]
    public int TimeoutTotalSegundos { get; set; } = 60;
    [Range(1, 30)]
    public int TimeoutCierreSegundos { get; set; } = 5;

    public void Validar()
    {
        Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);

        if (string.IsNullOrWhiteSpace(Host) || Host.Any(char.IsControl) || Puerto is < 1 or > 65535
            || !Enum.IsDefined(ModoTls) || string.IsNullOrWhiteSpace(Usuario)
            || string.IsNullOrWhiteSpace(Contrasena) || !MailboxAddress.TryParse(Remitente, out MailboxAddress? direccion)
            || !string.Equals(direccion.Address, Remitente, StringComparison.Ordinal)
            || NombreVisible.Any(char.IsControl))
        {
            throw new ValidationException("La configuración SMTP está incompleta o es inválida.");
        }
    }
}
