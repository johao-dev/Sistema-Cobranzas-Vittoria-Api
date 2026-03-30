using Cobranzas_Vittoria.Dtos.Auth;

namespace Cobranzas_Vittoria.Interfaces;

public interface IAuthRepository
{
    Task<LoginResponseDto?> LoginAsync(string usuarioLogin, string password);
}
