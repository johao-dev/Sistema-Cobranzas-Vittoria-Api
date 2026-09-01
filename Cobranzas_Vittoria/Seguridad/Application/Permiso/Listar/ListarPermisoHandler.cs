using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Listar;

public class ListarPermisoHandler
{
    private readonly IPermisoRepository _permisoRepository;
    private readonly ILogger<ListarPermisoHandler> _logger;

    public ListarPermisoHandler(
        IPermisoRepository permisoRepository,
        ILogger<ListarPermisoHandler> logger)
    {
        _permisoRepository = permisoRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<PermisoDto>> HandleAsync(ListarPermisoQuery query)
    {
        _logger.LogInformation("Listando permisos con estado activo={Activo}", query.Activo);

        var permisos = await _permisoRepository.GetAllAsync(query.Activo);
        var resultado = permisos.ToList();

        _logger.LogInformation("Se encontraron {Cantidad} permisos", resultado.Count);
        _logger.LogDebug("Permisos listados: {@Permisos}", resultado);

        return resultado.Select(p => new PermisoDto(
            p.IdPermiso,
            p.Codigo,
            p.Nombre,
            p.Descripcion,
            p.Activo,
            p.FechaCreacion,
            p.UsuarioCreacion,
            p.FechaModificacion,
            p.UsuarioModificacion));
    }
}
