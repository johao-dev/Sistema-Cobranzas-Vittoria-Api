using Cobranzas_Vittoria.Dtos.Auth;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _repository;

    public AuthService(IAuthRepository repository)
    {
        _repository = repository;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto)
    {
        var usuario = (dto.UsuarioLogin ?? string.Empty).Trim();
        var password = dto.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            return null;

        return await _repository.LoginAsync(usuario, password);
    }
}
