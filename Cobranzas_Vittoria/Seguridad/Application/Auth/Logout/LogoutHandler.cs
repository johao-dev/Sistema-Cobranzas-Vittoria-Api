using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Auth.Logout;

public sealed class LogoutHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(IRefreshTokenRepository refreshTokenRepository, IRefreshTokenService refreshTokenService,
        ILogger<LogoutHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task HandleAsync(string refreshToken)
    {
        _logger.LogInformation("Solicitud de cierre de sesión recibida.");
        var token = await _refreshTokenRepository.GetByTokenHashAsync(_refreshTokenService.CalcularHash(refreshToken));
        if (token?.EstaActivo(DateTime.UtcNow) == true)
        {
            await _refreshTokenRepository.RevokeAsync(token.IdRefreshToken, DateTime.UtcNow);
            _logger.LogInformation("Sesión cerrada para IdUsuario={IdUsuario}.", token.IdUsuario);
        }
    }
}
