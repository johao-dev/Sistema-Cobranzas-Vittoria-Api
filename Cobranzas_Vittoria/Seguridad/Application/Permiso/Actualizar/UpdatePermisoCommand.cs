namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Actualizar;

/// <summary>
/// Comando para actualizar parcialmente un permiso.
/// Solo Nombre y Descripcion son editables; Codigo no se puede cambiar.
/// </summary>
public sealed record UpdatePermisoCommand(
    int IdPermiso,
    string? Nombre,
    string? Descripcion);
