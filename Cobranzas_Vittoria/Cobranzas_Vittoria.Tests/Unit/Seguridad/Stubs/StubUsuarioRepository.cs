using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Domain.Model;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;

/// <summary>
/// Stub manual de <see cref="IUsuarioRepository"/> con colecciones mutables.
/// Sigue la convencion del proyecto de no usar Moq/NSubstitute.
///
/// Cada test puede armar su escenario agregando usuarios a <see cref="Usuarios"/>
/// o sobreescribiendo los callbacks <see cref="OnAddAsync"/>,
/// <see cref="OnUpdateAsync"/>, <see cref="OnAsignarRolesAsync"/>
/// y <see cref="OnQuitarRolAsync"/>.
/// </summary>
public sealed class StubUsuarioRepository : IUsuarioRepository
{
    public List<Usuario> Usuarios { get; } = new();

    /// <summary>Permite simular fallos o comportamiento custom en <see cref="AddAsync"/>.</summary>
    public Func<Usuario, Task<Usuario>>? OnAddAsync { get; set; }

    /// <summary>Permite simular fallos o comportamiento custom en <see cref="UpdateAsync"/>.</summary>
    public Func<Usuario, Task<Usuario>>? OnUpdateAsync { get; set; }

    /// <summary>Permite observar o simular <see cref="AsignarRolesAsync"/>.</summary>
    public Func<int, IEnumerable<int>, Task>? OnAsignarRolesAsync { get; set; }

    /// <summary>Permite observar o simular <see cref="QuitarRolAsync"/>.</summary>
    public Func<int, int, Task>? OnQuitarRolAsync { get; set; }

    public Task<Usuario?> GetByIdAsync(int idUsuario)
        => Task.FromResult(Usuarios.FirstOrDefault(u => u.IdUsuario == idUsuario));

    public Task<Usuario?> GetByIdWithRolesAsync(int idUsuario)
        => Task.FromResult(Usuarios.FirstOrDefault(u => u.IdUsuario == idUsuario));

    public Task<Usuario?> GetByCorreoAsync(string correo)
        => Task.FromResult(Usuarios.FirstOrDefault(u =>
            string.Equals(u.Correo.Value, correo, StringComparison.OrdinalIgnoreCase)));

    public Task<Usuario?> GetByLoginAsync(string usuarioLogin)
        => Task.FromResult(Usuarios.FirstOrDefault(u =>
            string.Equals(u.UsuarioLogin, usuarioLogin, StringComparison.OrdinalIgnoreCase)));

    public Task<IEnumerable<Usuario>> GetAllAsync(bool? activo = true)
    {
        IEnumerable<Usuario> q = Usuarios;
        if (activo.HasValue) q = q.Where(u => u.Activo == activo.Value);
        return Task.FromResult(q.AsEnumerable());
    }

    public Task<Usuario> AddAsync(Usuario usuario)
    {
        if (OnAddAsync is not null)
            return OnAddAsync(usuario);

        var nuevoId = Usuarios.Count == 0 ? 1 : Usuarios.Max(u => u.IdUsuario) + 1;
        var nuevo = Usuario.Reconstruir(
            nuevoId,
            usuario.Nombres,
            usuario.Apellidos,
            usuario.Correo.Value,
            usuario.UsuarioLogin,
            usuario.PasswordHash,
            usuario.Activo,
            usuario.UsuarioCreacion ?? "test",
            usuario.FechaCreacion ?? DateTime.UtcNow);

        // Conserva los roles pre-cargados en el dominio (si los hubiera).
        foreach (var rol in usuario.Roles)
        {
            nuevo.AsignarRol(rol);
        }

        Usuarios.Add(nuevo);
        return Task.FromResult(nuevo);
    }

    public Task<Usuario> UpdateAsync(Usuario usuario)
    {
        if (OnUpdateAsync is not null)
            return OnUpdateAsync(usuario);

        var existente = Usuarios.FirstOrDefault(u => u.IdUsuario == usuario.IdUsuario)
            ?? throw new InvalidOperationException($"Usuario con Id {usuario.IdUsuario} no encontrado.");

        Usuarios.Remove(existente);
        Usuarios.Add(usuario);
        return Task.FromResult(usuario);
    }

    public Task AsignarRolesAsync(int idUsuario, IEnumerable<int> idRoles)
    {
        if (OnAsignarRolesAsync is not null)
            return OnAsignarRolesAsync(idUsuario, idRoles);

        var usuario = Usuarios.FirstOrDefault(u => u.IdUsuario == idUsuario);
        if (usuario is null)
            throw new InvalidOperationException($"Usuario con Id {idUsuario} no encontrado.");

        return Task.CompletedTask;
    }

    public Task QuitarRolAsync(int idUsuario, int idRol)
    {
        if (OnQuitarRolAsync is not null)
            return OnQuitarRolAsync(idUsuario, idRol);

        return Task.CompletedTask;
    }

    public void Add(
        int idUsuario,
        string nombres,
        string apellidos,
        string correo,
        string usuarioLogin,
        string passwordHash = "password",
        bool activo = true,
        string usuarioCreacion = "test")
        => Usuarios.Add(Usuario.Reconstruir(
            idUsuario,
            nombres,
            apellidos,
            correo,
            usuarioLogin,
            passwordHash,
            activo,
            usuarioCreacion,
            DateTime.UtcNow));
}
