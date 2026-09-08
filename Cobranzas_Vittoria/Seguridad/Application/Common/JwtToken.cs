namespace Cobranzas_Vittoria.Seguridad.Application.Common;

public sealed record JwtToken(string Token, DateTime ExpiracionUtc);
