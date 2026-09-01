using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.ObtenerPorId;

public class ObtenerPorIdHandler
{
    private readonly IPermisoRepository _permisoRepository;
    private readonly ILogger<ObtenerPorIdHandler> _logger;

    public ObtenerPorIdHandler(
        IPermisoRepository permisoRepository,
        ILogger<ObtenerPorIdHandler> logger)
    {
        _permisoRepository = permisoRepository;
        _logger = logger;
    }

    public async Task<ObtenerPorIdResult> HandleAsync(ObtenerPorIdQuery query)
    {
        _logger.LogInformation(
            "Consultando permiso por IdPermiso={IdPermiso}",
            query.IdPermiso);

        Domain.Model.Permiso? permiso = await _permisoRepository.GetByIdAsync(query.IdPermiso)
            ?? throw new KeyNotFoundException($"Permiso con Id {query.IdPermiso} no encontrado.");

        _logger.LogInformation(
            "Permiso encontrado: IdPermiso={IdPermiso}, Codigo={Codigo}",
            permiso.IdPermiso,
            permiso.Codigo);

        return new ObtenerPorIdResult(
            permiso.IdPermiso,
            permiso.Codigo,
            permiso.Nombre,
            permiso.Descripcion,
            permiso.Activo,
            permiso.FechaCreacion,
            permiso.UsuarioCreacion,
            permiso.FechaModificacion,
            permiso.UsuarioModificacion);
    }
}