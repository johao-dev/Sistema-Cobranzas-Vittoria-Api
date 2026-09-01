using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.ObtenerPorId;

public class ObtenerPorIdHandler
{
    private readonly IPermisoRepository _permisoRepository;

    public ObtenerPorIdHandler(IPermisoRepository permisoRepository)
    {
        _permisoRepository = permisoRepository;
    }

    public async Task<ObtenerPorIdResult> HandleAsync(ObtenerPorIdQuery query)
    {
        var permiso = await _permisoRepository.GetByIdAsync(query.IdPermiso)
            ?? throw new KeyNotFoundException($"Permiso con Id {query.IdPermiso} no encontrado.");
        
        return new ObtenerPorIdResult(
            permiso.IdPermiso,
            permiso.Codigo,
            permiso.Nombre,
            permiso.Descripcion,
            permiso.Activo,
            permiso.FechaCreacion,
            permiso.UsuarioCreacion);
    }
}