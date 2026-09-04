using Cobranzas_Vittoria.Seguridad.Domain.Model;

namespace Cobranzas_Vittoria.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByIdAsync(int idUsuario);

    Task<Usuario?> GetByIdWithRolesAsync(int idUsuario);

    Task<Usuario?> GetByCorreoAsync(string correo);

    Task<IEnumerable<Usuario>> GetAllAsync(bool? activo = true);

    Task<Usuario> AddAsync(Usuario usuario);

    Task AsignarRolesAsync(int idUsuario, IEnumerable<int> idRoles);

    Task QuitarRolAsync(int idUsuario, int idRol);

    Task<Usuario> UpdateAsync(Usuario usuario);
}
