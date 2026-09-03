namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Crear;

public sealed record CreateRolResult(
    int IdRol,
    string Nombre,
    string Descripcion,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion);
