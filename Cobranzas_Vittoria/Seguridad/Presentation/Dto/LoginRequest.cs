namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record LoginRequest(string UsernameOrEmail, string Password);