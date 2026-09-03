namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record ListarRolResponse(
    IEnumerable<RolResponse> Roles);
