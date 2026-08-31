using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.Seguridad.Domain.Persistence;

public interface IPermisoRepository
{
    // Operaciones CRUD por el momento.

    Task<PermisoEntity> GetByIdAsync(int idPermiso);

    Task<IEnumerable<PermisoEntity>> GetAllAsync();

    Task AddAsync(PermisoEntity permiso);

    Task UpdateAsync(PermisoEntity permiso);

    Task DeleteAsync(int idPermiso);
}
