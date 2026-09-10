using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using RolDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Rol;

namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Obtener.ConPermisos;

public class ObtenerRolConPermisosHandler
{
    private readonly IRolRepository _rolRepository;
    private readonly ILogger<ObtenerRolConPermisosHandler> _logger;

    public ObtenerRolConPermisosHandler(
        IRolRepository rolRepository,
        ILogger<ObtenerRolConPermisosHandler> logger)
    {
        _rolRepository = rolRepository;
        _logger = logger;
    }

    public async Task<ObtenerRolConPermisosResult> HandleAsync(ObtenerRolQuery query)
    {
        _logger.LogInformation(
            "Consultando rol por IdRol={RolId}",
            query.IdRol);
        
        RolDomain? rol = await _rolRepository.GetByIdWithPermisosAsync(query.IdRol)
            ?? throw new KeyNotFoundException($"Rol con Id {query.IdRol} no encontrado.");

        _logger.LogInformation("Rol encontrado: IdRol={IdRol}, Nombre={Nombre}",
            rol.IdRol,
            rol.Nombre);
        
        return new ObtenerRolConPermisosResult(
            rol.IdRol,
            rol.Nombre,
            rol.Descripcion,
            rol.Activo,
            rol.Permisos.Select(p => new PermisoAsignadoResult(
                p.IdPermiso,
                p.Codigo,
                p.Nombre)));
    }
}
