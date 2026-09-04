namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.AsignarRoles;

public sealed record AsignarRolesCommand(
    int IdUsuario,
    IEnumerable<int> IdRoles);
