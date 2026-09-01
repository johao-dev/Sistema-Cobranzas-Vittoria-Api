namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Actualizar;

public sealed record UpdatePermisoResult(
    int IdPermiso,
    string Codigo,
    string Nombre,
    string Descripcion,
    bool Activo,
    DateTime? FechaModificacion,
    string? UsuarioModificacion
);
