namespace Cobranzas_Vittoria.Seguridad.Application.Common;
    
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hashedPassword);
}