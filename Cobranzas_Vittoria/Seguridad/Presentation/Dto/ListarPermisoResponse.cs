namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record ListarPermisoResponse(
    IEnumerable<PermisoResponse> Permisos
);