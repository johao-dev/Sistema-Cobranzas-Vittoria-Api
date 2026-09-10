namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record RolConPermisosResponse(
    int IdRol,
    string Nombre,
    string Descripcion,
    bool Activo,
    IEnumerable<PermisoAsignadoResponse> Permisos
);
