namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record PermisoResponse
(
    int IdPermiso,
    string Codigo,
    string Nombre,
    string Descripcion,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion,
    DateTime? FechaModificacion,
    string? UsuarioModificacion
);