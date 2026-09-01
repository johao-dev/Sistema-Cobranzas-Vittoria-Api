namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;

public sealed record CreatePermisoResult(
    int IdPermiso,
    string Codigo,
    string Nombre,
    string Descripcion,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion
);