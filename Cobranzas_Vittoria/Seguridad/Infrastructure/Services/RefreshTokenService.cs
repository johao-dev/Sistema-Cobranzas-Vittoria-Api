using System.Security.Cryptography;
using System.Text;
using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly IConfiguration _configuration;

    public RefreshTokenService(IConfiguration configuration) => _configuration = configuration;

    public string GenerarToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string CalcularHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public DateTime ObtenerExpiracionUtc()
    {
        int dias = _configuration.GetValue<int?>("Jwt:RefreshExpireDays") ?? 7;
        return DateTime.UtcNow.AddDays(dias);
    }
}
