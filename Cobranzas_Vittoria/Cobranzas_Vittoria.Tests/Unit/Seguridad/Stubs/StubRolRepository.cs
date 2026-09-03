using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;

/// <summary>
/// Stub manual de <see cref="IRolRepository"/> con colecciones mutables.
/// Sigue la convencion del proyecto de no usar Moq/NSubstitute.
/// </summary>
public sealed class StubRolRepository : IRolRepository
{
    public List<Rol> Roles { get; } = new();

    public Func<Rol, Task<Rol>>? OnAddAsync { get; set; }
    public Func<Rol, Task<Rol>>? OnUpdateAsync { get; set; }
    public Func<int, Task>? OnDeleteAsync { get; set; }

    public Task<Rol?> GetByIdAsync(int idRol)
        => Task.FromResult(Roles.FirstOrDefault(r => r.IdRol == idRol));

    public Task<Rol?> GetByNombreAsync(string nombre)
        => Task.FromResult(Roles.FirstOrDefault(r =>
            string.Equals(r.Nombre, nombre, StringComparison.OrdinalIgnoreCase)));

    public Task<IEnumerable<Rol>> GetAllAsync(bool? activo = true)
    {
        IEnumerable<Rol> q = Roles;
        if (activo.HasValue) q = q.Where(r => r.Activo == activo.Value);
        return Task.FromResult(q.AsEnumerable());
    }

    public Task<Rol> AddAsync(Rol rol)
    {
        if (OnAddAsync is not null)
            return OnAddAsync(rol);

        var nuevoId = Roles.Count == 0 ? 1 : Roles.Max(r => r.IdRol) + 1;
        var nuevo = Rol.Reconstruir(
            nuevoId,
            rol.Nombre,
            rol.Descripcion,
            rol.Activo,
            rol.FechaCreacion,
            rol.UsuarioCreacion);

        Roles.Add(nuevo);
        return Task.FromResult(nuevo);
    }

    public Task<Rol> UpdateAsync(Rol rol)
    {
        if (OnUpdateAsync is not null)
            return OnUpdateAsync(rol);

        var existente = Roles.FirstOrDefault(r => r.IdRol == rol.IdRol)
            ?? throw new InvalidOperationException($"Rol con Id {rol.IdRol} no encontrado.");

        Roles.Remove(existente);
        Roles.Add(rol);
        return Task.FromResult(rol);
    }

    public Task DeleteAsync(int idRol)
    {
        if (OnDeleteAsync is not null)
            return OnDeleteAsync(idRol);

        var existente = Roles.FirstOrDefault(r => r.IdRol == idRol);
        if (existente is not null)
            Roles.Remove(existente);

        return Task.CompletedTask;
    }

    public void Add(int idRol, string nombre, string descripcion = "", bool activo = true)
        => Roles.Add(Rol.Reconstruir(idRol, nombre, descripcion, activo));
}
