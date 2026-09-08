using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Mapper;
using Dapper;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;

public sealed class RefreshTokenRepository : RepositoryBase, IRefreshTokenRepository
{
    public RefreshTokenRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task AddAsync(RefreshToken refreshToken)
    {
        using IDbConnection db = Open();
        RefreshTokenEntity entity = RefreshTokenMapper.ToEntity(refreshToken);
        await db.ExecuteAsync(
            "seguridad.usp_RefreshToken_Insert",
            new
            {
                entity.IdUsuario,
                entity.TokenHash,
                entity.FechaExpiracionUtc,
                entity.FechaCreacionUtc
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
    {
        using IDbConnection db = Open();
        RefreshTokenEntity? entity = await db.QueryFirstOrDefaultAsync<RefreshTokenEntity>(
            "seguridad.usp_RefreshToken_GetByHash",
            new { TokenHash = tokenHash },
            commandType: CommandType.StoredProcedure);
        return entity is null ? null : RefreshTokenMapper.ToDomain(entity);
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
