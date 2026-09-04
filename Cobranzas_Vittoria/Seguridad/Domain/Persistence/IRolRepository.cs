using Cobranzas_Vittoria.Seguridad.Domain.Model;

namespace Cobranzas_Vittoria.Seguridad.Domain.Persistence;

// TODO: Los métodos comentados deben ser implementados en otra rama
public interface IRolRepository
{
    Task<Rol?> GetByIdAsync(int idRol);

    // Task<Rol?> GetBydIdWithPermisosAsync(int idRol);

    Task<Rol?> GetByNombreAsync(string nombre);

    Task<IEnumerable<Rol>> GetAllAsync(bool? activo = true);

    Task<Rol> AddAsync(Rol rol);

    // Task AsignarPermisosAsync(int idRol, IEnumerable<int> idPermisos);

    Task<Rol> UpdateAsync(Rol rol);
    
    Task DeleteAsync(int idRol);

    // Task QuitarPermisoAsync(int idRol, int idPermiso);
}
