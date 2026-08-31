namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record UpdatePermisoRequest
(
    // actualiza solo los campos proporcionados
    string? Codigo,
    string? Nombre,
    string? Descripcion
);
