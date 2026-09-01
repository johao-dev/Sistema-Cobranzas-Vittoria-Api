namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Listar;

/// <summary>
/// DTO de lectura que representa un permiso en la capa de aplicacion.
/// </summary>
public sealed record PermisoDto(
    int IdPermiso,
    string Codigo,
    string Nombre,
    string Descripcion,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion,
    DateTime? FechaModificacion,
    string? UsuarioModificacion);
