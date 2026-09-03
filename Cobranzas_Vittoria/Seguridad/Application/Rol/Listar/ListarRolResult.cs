namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Listar;

/// <summary>
/// DTO de lectura que representa un rol en la capa de aplicacion.
/// </summary>
public sealed record ListarRolResult(
    int IdRol,
    string Nombre,
    string Descripcion,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion,
    DateTime? FechaModificacion,
    string? UsuarioModificacion);
