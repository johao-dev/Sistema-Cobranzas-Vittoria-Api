using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Mapper;

public static class RefreshTokenMapper
{
    public static RefreshTokenEntity ToEntity(RefreshToken token) => new()
    {
        IdRefreshToken = token.IdRefreshToken,
        IdUsuario = token.IdUsuario,
        TokenHash = token.TokenHash,
        FechaExpiracionUtc = token.FechaExpiracionUtc,
        FechaCreacionUtc = token.FechaCreacionUtc,
        FechaRevocacionUtc = token.FechaRevocacionUtc,
        ReemplazadoPorHash = token.ReemplazadoPorHash
    };

    public static RefreshToken ToDomain(RefreshTokenEntity entity) => new()
    {
        IdRefreshToken = entity.IdRefreshToken,
        IdUsuario = entity.IdUsuario,
        TokenHash = entity.TokenHash,
        FechaExpiracionUtc = entity.FechaExpiracionUtc,
        FechaCreacionUtc = entity.FechaCreacionUtc,
        FechaRevocacionUtc = entity.FechaRevocacionUtc,
        ReemplazadoPorHash = entity.ReemplazadoPorHash
    };
}
