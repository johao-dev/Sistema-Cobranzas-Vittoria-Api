namespace Cobranzas_Vittoria.Seguridad.Application.Auth.Login;
using System;

public sealed record LoginResult(string Token, DateTime Expiration, string RefreshToken);