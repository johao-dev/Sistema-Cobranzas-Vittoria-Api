namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

/// <summary>
/// Request para actualizar parcialmente un permiso.
/// Solo Nombre y Descripcion son editables.
/// </summary>
public sealed record UpdatePermisoRequest
(
    string? Nombre,
    string? Descripcion
);
