namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;

public sealed record CreatePermisoResult(
    int Id,
    string Codigo,
    string Nombre,
    string Descripcion,
    bool Activo
);