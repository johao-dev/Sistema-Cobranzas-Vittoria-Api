namespace Cobranzas_Vittoria.Seguridad.Application.Auth.Login;

public sealed record LoginCommand(string UsernameOrEmail, string Password);