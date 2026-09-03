namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

/// <summary>
/// Request para actualizar parcialmente un rol.
/// </summary>
public sealed record UpdateRolRequest
(
    string? Nombre,
    string? Descripcion,
    bool? Activo
);
