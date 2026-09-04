namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record UpdateUsuarioRequest
(
    string? Nombres,
    string? Apellidos,
    string? Correo,
    string? UsuarioLogin,
    string? PasswordHash,
    bool? Activo
);
