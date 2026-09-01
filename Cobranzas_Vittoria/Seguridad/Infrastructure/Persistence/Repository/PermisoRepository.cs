using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;

public class PermisoRepository : RepositoryBase, IPermisoRepository
{
    public PermisoRepository(IDbConnectionFactory factory) : base(factory) { }


    public async Task<Permiso?> GetByIdAsync(int idPermiso)
    {
        return null;
    }

    public async Task<IEnumerable<Permiso>> GetAllAsync()
    {
        return null;
    }


    public async Task AddAsync(Permiso permiso) {}

    public async Task UpdateAsync(Permiso permiso) {}

    public async Task DeleteAsync(int idPermiso) {}
}
