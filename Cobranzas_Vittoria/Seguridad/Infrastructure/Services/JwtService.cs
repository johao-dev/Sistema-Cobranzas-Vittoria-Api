using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Services;

public sealed class JwtService : IJwtService
{
    private readonly JwtOptions _options;

    public JwtService(IOptions<JwtOptions> options) => _options = options.Value;

    public JwtToken GenerarToken(Usuario usuario, IEnumerable<Rol> roles)
    {
        string key = _options.Key;
        string issuer = _options.Issuer;
        string audience = _options.Audience;
        int minutos = _options.ExpireMinutes;

        List<Rol> rolesActivos = roles.Where(r => r.Activo).ToList();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new(ClaimTypes.Name, usuario.UsuarioLogin),
            new(ClaimTypes.Email, usuario.Correo.Value),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(rolesActivos.Select(r => new Claim(ClaimTypes.Role, r.Nombre)));
        claims.AddRange(rolesActivos
            .SelectMany(r => r.Permisos)
            .Where(p => p.Activo)
            .Select(p => p.Codigo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(codigo => new Claim("permission", codigo)));

        DateTime expiracionUtc = DateTime.UtcNow.AddMinutes(minutos);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore: DateTime.UtcNow,
            expires: expiracionUtc,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new JwtToken(new JwtSecurityTokenHandler().WriteToken(token), expiracionUtc);
    }
}
