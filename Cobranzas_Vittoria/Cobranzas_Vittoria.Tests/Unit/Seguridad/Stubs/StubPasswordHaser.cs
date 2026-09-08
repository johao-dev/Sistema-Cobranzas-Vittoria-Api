using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;

public sealed class StubPasswordHasher : IPasswordHasher
{
    public const string HashPrefix = "hashed:";

    public string? LastPasswordHashed { get; private set; }

    public string Hash(string password)
    {
        LastPasswordHashed = password;
        return $"{HashPrefix}{password}";
    }

    public bool Verify(string password, string hashedPassword)
    {
        return hashedPassword == $"{HashPrefix}{password}";
    }
}
