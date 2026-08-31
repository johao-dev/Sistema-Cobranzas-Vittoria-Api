using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;

public class PermisoRepository : RepositoryBase, IPermisoRepository
{
    public PermisoRepository(IDbConnectionFactory factory) : base(factory) { }


    public async Task<PermisoEntity> GetByIdAsync(int idPermiso)
    {
        return null;
    }

    public async Task<IEnumerable<PermisoEntity>> GetAllAsync()
    {
        return null;
    }


    public async Task AddAsync(PermisoEntity permiso) {}

    public async Task UpdateAsync(PermisoEntity permiso) {}

    public async Task DeleteAsync(int idPermiso) {}
}
