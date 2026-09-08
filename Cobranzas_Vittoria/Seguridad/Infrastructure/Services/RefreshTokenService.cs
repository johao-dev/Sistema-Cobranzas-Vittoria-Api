using System.Security.Cryptography;
using System.Text;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Microsoft.Extensions.Options;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly JwtOptions _options;

    public RefreshTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public string GenerarToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string CalcularHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public DateTime ObtenerExpiracionUtc()
    {
        return DateTime.UtcNow.AddDays(_options.RefreshExpireDays);
    }
}
