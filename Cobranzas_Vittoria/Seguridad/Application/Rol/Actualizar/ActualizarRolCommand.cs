namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Actualizar;

/// <summary>
/// Comando para actualizar parcialmente un rol.
/// </summary>
public sealed record ActualizarRolCommand(
    int IdRol,
    string? Nombre,
    string? Descripcion,
    bool? Activo);
