namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record UsuarioResponse
(
    int IdUsuario,
    string Nombres,
    string Apellidos,
    string Correo,
    string UsuarioLogin,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion
);
