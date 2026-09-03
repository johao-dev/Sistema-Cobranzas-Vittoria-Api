using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Listar;

public class ListarRolHandler
{
    private readonly IRolRepository _rolRepository;
    private readonly ILogger<ListarRolHandler> _logger;

    public ListarRolHandler(
        IRolRepository rolRepository,
        ILogger<ListarRolHandler> logger)
    {
        _rolRepository = rolRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<ListarRolResult>> HandleAsync(ListarRolQuery query)
    {
        _logger.LogInformation("Listando roles con estado activo={Activo}", query.Activo ?? true);

        var roles = await _rolRepository.GetAllAsync(query.Activo);
        var resultado = roles.ToList();

        _logger.LogInformation("Se encontraron {Cantidad} roles", resultado.Count);
        _logger.LogDebug("Roles listados: {@Roles}", resultado);

        return resultado.Select(r => new ListarRolResult(
            r.IdRol,
            r.Nombre,
            r.Descripcion,
            r.Activo,
            r.FechaCreacion,
            r.UsuarioCreacion,
            r.FechaModificacion,
            r.UsuarioModificacion));
    }
}
