using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;

public sealed class StubPasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        // Retorna el password tal cual, sin hashear.
        return password;
    }

    public bool Verify(string hashedPassword, string providedPassword)
    {
        // Compara los passwords tal cual, sin hashear.
        return hashedPassword == providedPassword;
    }
}