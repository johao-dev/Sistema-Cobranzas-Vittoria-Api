namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record RolResponse
(
    int IdRol,
    string Nombre,
    string Descripcion,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion,
    DateTime? FechaModificacion,
    string? UsuarioModificacion
);
