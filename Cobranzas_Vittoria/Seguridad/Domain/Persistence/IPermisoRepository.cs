using Cobranzas_Vittoria.Seguridad.Domain.Model;

namespace Cobranzas_Vittoria.Seguridad.Domain.Persistence;

public interface IPermisoRepository
{
    Task<Permiso?> GetByIdAsync(int idPermiso);

    Task<IEnumerable<Permiso>> GetAllAsync(bool activo = true);

    Task<Permiso> UpdateAsync(Permiso permiso);
}
