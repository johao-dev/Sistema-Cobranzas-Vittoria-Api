namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;

public sealed record CreatePermisoCommand(
    string Codigo,
    string Nombre,
    string Descripcion
);