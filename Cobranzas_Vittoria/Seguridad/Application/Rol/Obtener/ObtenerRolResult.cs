namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Obtener;

public sealed record ObtenerRolResult(
    int IdRol,
    string Nombre,
    string Descripcion,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion,
    DateTime? FechaModificacion,
    string? UsuarioModificacion);
