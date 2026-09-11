using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Cobranzas_Vittoria.Tests.Integration.Common;

/// <summary>Genera tokens compatibles con la configuracion JWT de la API de prueba.</summary>
public static class JwtTestTokenFactory
{
    public const string UsuarioLogin = "integration-test-user";

    public static string CrearToken(
        int idUsuario = 1,
        string usuarioLogin = UsuarioLogin,
        string correo = "integration-test-user@local",
        DateTime? expiraEnUtc = null,
        IEnumerable<string>? permisos = null)
    {
        string key = Environment.GetEnvironmentVariable("Jwt__Key")
            ?? throw new InvalidOperationException("Jwt__Key no esta configurada para los tests.");
        string issuer = Environment.GetEnvironmentVariable("Jwt__Issuer")
            ?? throw new InvalidOperationException("Jwt__Issuer no esta configurado para los tests.");
        string audience = Environment.GetEnvironmentVariable("Jwt__Audience")
            ?? throw new InvalidOperationException("Jwt__Audience no esta configurado para los tests.");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()),
            new Claim(ClaimTypes.Name, usuarioLogin),
            new Claim(ClaimTypes.Email, correo),
            new Claim(ClaimTypes.Role, "Administrador")
        };
        claims.AddRange((permisos ?? TodosLosPermisosRequerimientos)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(permission => new Claim(PermissionAuthorizationHandler.ClaimType, permission)));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        DateTime expiration = expiraEnUtc ?? DateTime.UtcNow.AddMinutes(10);
        DateTime notBefore = expiration <= DateTime.UtcNow
            ? expiration.AddMinutes(-1)
            : DateTime.UtcNow.AddMinutes(-1);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore: notBefore,
            expires: expiration,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static IReadOnlyList<string> TodosLosPermisosRequerimientos { get; } =
    [
        Permisos.Requerimientos.Ver,
        Permisos.Requerimientos.Crear,
        Permisos.Requerimientos.EditarBorrador,
        Permisos.Requerimientos.EditarCantidadesAlmacen,
        Permisos.Requerimientos.Enviar,
        Permisos.Requerimientos.ProcesarStock,
        Permisos.Requerimientos.VerDepuracionAlmacen,
        Permisos.Requerimientos.Aprobar,
        Permisos.Requerimientos.Rechazar,
        Permisos.Requerimientos.EnviarCompras
    ];
}
