using Cobranzas_Vittoria.Dtos.Auth;

namespace Cobranzas_Vittoria.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto);
}
