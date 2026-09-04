using System.Net.Mail;

namespace Cobranzas_Vittoria.Seguridad.Domain.ValueObject;

public class Email
{
    public string Value { get; private set; } = string.Empty;

    private Email() { }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El correo no puede estar vacío.", nameof(value));

        try
        {
            MailAddress addr = new MailAddress(value);
            if (addr.Address != value)
                throw new ArgumentException("El correo no tiene un formato válido.", nameof(value));
        }
        catch
        {
            throw new ArgumentException("El correo no tiene un formato válido.", nameof(value));
        }

        Value = value;
    }
}