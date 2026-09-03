using Cobranzas_Vittoria.Seguridad.Domain.Model;

namespace Cobranzas_Vittoria.Seguridad.Domain.Persistence;

public interface IRolRepository
{
    Task<Rol?> GetByIdAsync(int idRol);

    Task<Rol?> GetByNombreAsync(string nombre);

    Task<IEnumerable<Rol>> GetAllAsync(bool? activo = true);

    Task<Rol> AddAsync(Rol rol);

    Task<Rol> UpdateAsync(Rol rol);
    
    Task DeleteAsync(int idRol);
}
