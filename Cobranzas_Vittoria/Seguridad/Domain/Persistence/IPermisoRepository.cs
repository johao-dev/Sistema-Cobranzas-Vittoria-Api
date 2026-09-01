using Cobranzas_Vittoria.Seguridad.Domain.Model;

namespace Cobranzas_Vittoria.Seguridad.Domain.Persistence;

public interface IPermisoRepository
{
    Task<Permiso?> GetByIdAsync(int idPermiso);

    Task<IEnumerable<Permiso>> GetAllAsync(bool activo = true);

    Task<Permiso> AddAsync(Permiso permiso);

    Task<Permiso> UpdateAsync(Permiso permiso);

    Task DeleteAsync(int idPermiso);
}
