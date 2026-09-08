using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using UsuarioDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Usuario;
using RolDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Rol;

namespace Cobranzas_Vittoria.Seguridad.Application.Auth.Login;

public class LoginHandler
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRolRepository _rolRepository;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        IUsuarioRepository usuarioRepository,
        IPasswordHasher passwordHasher,
        IRolRepository rolRepository,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<LoginHandler> logger)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
        _rolRepository = rolRepository;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task<LoginResult> HandleAsync(LoginCommand command)
    {
        _logger.LogInformation("Intento de inicio de sesión recibido.");
        UsuarioDomain? usuario = await _usuarioRepository.GetByLoginAsync(command.UsernameOrEmail)
            ?? await _usuarioRepository.GetByCorreoAsync(command.UsernameOrEmail);

        if (usuario is null || !usuario.Activo || !_passwordHasher.Verify(command.Password, usuario.PasswordHash))
        {
            _logger.LogWarning("Inicio de sesión rechazado por credenciales inválidas.");
            throw new AutenticacionException();
        }

        UsuarioDomain usuarioConRoles = await _usuarioRepository.GetByIdWithRolesAsync(usuario.IdUsuario)
            ?? throw new AutenticacionException();
            
        List<RolDomain> roles = await ObtenerRolesConPermisosAsync(usuarioConRoles);
        JwtToken jwt = _jwtService.GenerarToken(usuarioConRoles, roles);
        string refreshToken = _refreshTokenService.GenerarToken();
        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            IdUsuario = usuarioConRoles.IdUsuario,
            TokenHash = _refreshTokenService.CalcularHash(refreshToken),
            FechaCreacionUtc = DateTime.UtcNow,
            FechaExpiracionUtc = _refreshTokenService.ObtenerExpiracionUtc()
        });

        _logger.LogInformation("Inicio de sesión exitoso para IdUsuario={IdUsuario}.", usuarioConRoles.IdUsuario);
        return new LoginResult(jwt.Token, jwt.ExpiracionUtc, refreshToken);
    }

    private async Task<List<RolDomain>> ObtenerRolesConPermisosAsync(UsuarioDomain usuario)
    {
        RolDomain?[] roles = await Task.WhenAll(usuario.Roles.Select(r => _rolRepository.GetByIdWithPermisosAsync(r.IdRol)));
        return roles.Where(r => r is not null).Cast<RolDomain>().ToList();
    }
}
