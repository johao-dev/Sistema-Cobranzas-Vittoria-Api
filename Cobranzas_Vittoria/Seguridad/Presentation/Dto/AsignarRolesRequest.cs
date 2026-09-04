namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record AsignarRolesRequest(
    IEnumerable<int> IdRoles);
