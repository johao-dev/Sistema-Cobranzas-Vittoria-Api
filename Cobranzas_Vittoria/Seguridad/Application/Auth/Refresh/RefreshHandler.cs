using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Application.Auth.Login;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
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
    private readonly ILogger<RefreshHandler> _logger;

    public RefreshHandler(IRefreshTokenRepository refreshTokenRepository, IRefreshTokenService refreshTokenService,
        IUsuarioRepository usuarioRepository, IRolRepository rolRepository, IJwtService jwtService,
        ILogger<RefreshHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
        _usuarioRepository = usuarioRepository;
        _rolRepository = rolRepository;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<LoginResult> HandleAsync(RefreshCommand command)
    {
        _logger.LogInformation("Solicitud de renovación de token recibida.");
        string hashActual = _refreshTokenService.CalcularHash(command.RefreshToken);
        RefreshToken refreshToken = await _refreshTokenRepository.GetByTokenHashAsync(hashActual)
            ?? throw new AutenticacionException();
        if (!refreshToken.EstaActivo(DateTime.UtcNow))
            throw new AutenticacionException();

        UsuarioDomain usuario = await _usuarioRepository.GetByIdWithRolesAsync(refreshToken.IdUsuario)
            ?? throw new AutenticacionException();
        if (!usuario.Activo)
            throw new AutenticacionException();

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

        _logger.LogInformation("Token renovado para IdUsuario={IdUsuario}.", usuario.IdUsuario);
        return new LoginResult(jwt.Token, jwt.ExpiracionUtc, nuevoRefreshToken);
    }
}
