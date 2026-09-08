namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;

public sealed class RefreshTokenEntity
{
    public int IdRefreshToken { get; set; }
    public int IdUsuario { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime FechaExpiracionUtc { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime? FechaRevocacionUtc { get; set; }
    public string? ReemplazadoPorHash { get; set; }
}
