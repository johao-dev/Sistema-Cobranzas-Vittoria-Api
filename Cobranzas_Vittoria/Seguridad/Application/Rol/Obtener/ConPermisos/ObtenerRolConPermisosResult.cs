namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Obtener.ConPermisos;

public sealed record ObtenerRolConPermisosResult(
    int IdRol,
    string Nombre,
    string Descripcion,
    bool Activo,
    IEnumerable<PermisoAsignadoResult> Permisos
);
