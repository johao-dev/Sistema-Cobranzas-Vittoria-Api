using Cobranzas_Vittoria.Seguridad.Domain.Model;

namespace Cobranzas_Vittoria.Seguridad.Domain.Persistence;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken);

    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
    
    Task RevokeAsync(int idRefreshToken, DateTime fechaRevocacionUtc, string? reemplazadoPorHash = null);
}
