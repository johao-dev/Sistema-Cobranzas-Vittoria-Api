using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Auth.Logout;

public sealed class LogoutHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenService _refreshTokenService;

    public LogoutHandler(IRefreshTokenRepository refreshTokenRepository, IRefreshTokenService refreshTokenService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
    }

    public async Task HandleAsync(string refreshToken)
    {
        var token = await _refreshTokenRepository.GetByTokenHashAsync(_refreshTokenService.CalcularHash(refreshToken));
        if (token?.EstaActivo(DateTime.UtcNow) == true)
            await _refreshTokenRepository.RevokeAsync(token.IdRefreshToken, DateTime.UtcNow);
    }
}
