using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Application.Auth.Login;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using UsuarioDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Usuario;
using RolDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Rol;

namespace Cobranzas_Vittoria.Seguridad.Application.Auth.Refresh;

public sealed class RefreshHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRolRepository _rolRepository;
    private readonly IJwtService _jwtService;

    public RefreshHandler(IRefreshTokenRepository refreshTokenRepository, IRefreshTokenService refreshTokenService,
        IUsuarioRepository usuarioRepository, IRolRepository rolRepository, IJwtService jwtService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
        _usuarioRepository = usuarioRepository;
        _rolRepository = rolRepository;
        _jwtService = jwtService;
    }

    public async Task<LoginResult> HandleAsync(RefreshCommand command)
    {
        string hashActual = _refreshTokenService.CalcularHash(command.RefreshToken);
        RefreshToken refreshToken = await _refreshTokenRepository.GetByTokenHashAsync(hashActual)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");
        if (!refreshToken.EstaActivo(DateTime.UtcNow))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        UsuarioDomain usuario = await _usuarioRepository.GetByIdWithRolesAsync(refreshToken.IdUsuario)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");
        if (!usuario.Activo)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        var roles = await Task.WhenAll(usuario.Roles.Select(r => _rolRepository.GetByIdWithPermisosAsync(r.IdRol)));
        JwtToken jwt = _jwtService.GenerarToken(usuario, roles.Where(r => r is not null).Cast<RolDomain>());
        string nuevoRefreshToken = _refreshTokenService.GenerarToken();
        string nuevoHash = _refreshTokenService.CalcularHash(nuevoRefreshToken);
        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            IdUsuario = usuario.IdUsuario,
            TokenHash = nuevoHash,
            FechaCreacionUtc = DateTime.UtcNow,
            FechaExpiracionUtc = _refreshTokenService.ObtenerExpiracionUtc()
        });
        await _refreshTokenRepository.RevokeAsync(refreshToken.IdRefreshToken, DateTime.UtcNow, nuevoHash);

        return new LoginResult(jwt.Token, jwt.ExpiracionUtc, nuevoRefreshToken);
    }
}
