using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Obtener;

public class ObtenerRolHandler
{
    private readonly IRolRepository _rolRepository;
    private readonly ILogger<ObtenerRolHandler> _logger;

    public ObtenerRolHandler(
        IRolRepository rolRepository,
        ILogger<ObtenerRolHandler> logger)
    {
        _rolRepository = rolRepository;
        _logger = logger;
    }

    public async Task<ObtenerRolResult> HandleAsync(ObtenerRolQuery query)
    {
        _logger.LogInformation(
            "Consultando rol por IdRol={IdRol}",
            query.IdRol);

        Domain.Model.Rol? rol = await _rolRepository.GetByIdAsync(query.IdRol)
            ?? throw new KeyNotFoundException($"Rol con Id {query.IdRol} no encontrado.");

        _logger.LogInformation(
            "Rol encontrado: IdRol={IdRol}, Nombre={Nombre}",
            rol.IdRol,
            rol.Nombre);

        return new ObtenerRolResult(
            rol.IdRol,
            rol.Nombre,
            rol.Descripcion,
            rol.Activo,
            rol.FechaCreacion,
            rol.UsuarioCreacion,
            rol.FechaModificacion,
            rol.UsuarioModificacion);
    }
}
