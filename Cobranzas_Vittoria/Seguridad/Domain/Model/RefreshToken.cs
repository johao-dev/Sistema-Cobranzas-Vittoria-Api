namespace Cobranzas_Vittoria.Seguridad.Domain.Model;

/// <summary>Refresh token persistido únicamente mediante su hash SHA-256.</summary>
public sealed class RefreshToken // TODO: Esto debería ir en Persistence/Entity para no mezclar Domain con Persistence.
{
    public int IdRefreshToken { get; init; }
    public int IdUsuario { get; init; }
    public string TokenHash { get; init; } = string.Empty;
    public DateTime FechaExpiracionUtc { get; init; }
    public DateTime FechaCreacionUtc { get; init; }
    public DateTime? FechaRevocacionUtc { get; init; }
    public string? ReemplazadoPorHash { get; init; }

    public bool EstaActivo(DateTime ahoraUtc) =>
        FechaRevocacionUtc is null && FechaExpiracionUtc > ahoraUtc;
}
