using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;

public class RolRepository : RepositoryBase, IRolRepository
{
    public RolRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<Rol?> GetByIdAsync(int idRol)
    {
        throw new NotImplementedException();
    }

    public async Task<Rol?> GetByNombreAsync(string nombre)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<Rol>> GetAllAsync(bool? activo = true)
    {
        throw new NotImplementedException();
    }

    public async Task<Rol> AddAsync(Rol rol)
    {
        throw new NotImplementedException();
    }

    public async Task<Rol> UpdateAsync(Rol rol)
    {
        throw new NotImplementedException();
    }

    public async Task DeleteAsync(int idRol)
    {
        throw new NotImplementedException();
    }
}
