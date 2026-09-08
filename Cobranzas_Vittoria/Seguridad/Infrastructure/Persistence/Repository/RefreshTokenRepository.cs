using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using Dapper;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;

// TODO: Agregar RefreshTokenEntity y RefreshTokenMapper para asegurar coherencia con el código existente.

public sealed class RefreshTokenRepository : RepositoryBase, IRefreshTokenRepository
{
    public RefreshTokenRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task AddAsync(RefreshToken refreshToken)
    {
        using IDbConnection db = Open();
        await db.ExecuteAsync(
            "seguridad.usp_RefreshToken_Insert",
            new
            {
                refreshToken.IdUsuario,
                refreshToken.TokenHash,
                refreshToken.FechaExpiracionUtc,
                refreshToken.FechaCreacionUtc
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
    {
        using IDbConnection db = Open();
        return await db.QueryFirstOrDefaultAsync<RefreshToken>(
            "seguridad.usp_RefreshToken_GetByHash",
            new { TokenHash = tokenHash },
            commandType: CommandType.StoredProcedure);
    }

    public async Task RevokeAsync(int idRefreshToken, DateTime fechaRevocacionUtc, string? reemplazadoPorHash = null)
    {
        using IDbConnection db = Open();
        await db.ExecuteAsync(
            "seguridad.usp_RefreshToken_Revoke",
            new { IdRefreshToken = idRefreshToken, FechaRevocacionUtc = fechaRevocacionUtc, ReemplazadoPorHash = reemplazadoPorHash },
            commandType: CommandType.StoredProcedure);
    }
}
