namespace Cobranzas_Vittoria.Seguridad.Application.Common;

/// <summary>Configuración compartida por validación, emisión y refresh de JWT.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpireMinutes { get; init; } = 60;
    public int RefreshExpireDays { get; init; } = 7;
}
