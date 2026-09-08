namespace Cobranzas_Vittoria.Seguridad.Application.Rol.AsignarPermisos;

public sealed record AsignarPermisosCommand(int IdRol, IEnumerable<int> IdPermisos);
