using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;

/// <summary>
/// Stub manual de <see cref="IPermisoRepository"/> con colecciones mutables.
/// Sigue la convencion del proyecto de no usar Moq/NSubstitute.
///
/// Cada test puede armar su escenario agregando permisos a <see cref="Permisos"/>
/// o sobreescribiendo los callbacks <see cref="OnAddAsync"/>,
/// <see cref="OnUpdateAsync"/> y <see cref="OnDeleteAsync"/>.
/// </summary>
public sealed class StubPermisoRepository : IPermisoRepository
{
    public List<Permiso> Permisos { get; } = new();

    /// <summary>Permite simular fallos o comportamiento custom en <see cref="AddAsync"/>.</summary>
    public Func<Permiso, Task<Permiso>>? OnAddAsync { get; set; }

    /// <summary>Permite simular fallos o comportamiento custom en <see cref="UpdateAsync"/>.</summary>
    public Func<Permiso, Task<Permiso>>? OnUpdateAsync { get; set; }

    /// <summary>Permite observar o simular <see cref="DeleteAsync"/>.</summary>
    public Func<int, Task>? OnDeleteAsync { get; set; }

    public Task<Permiso?> GetByIdAsync(int idPermiso)
        => Task.FromResult(Permisos.FirstOrDefault(p => p.IdPermiso == idPermiso));

    public Task<Permiso?> GetByCodigoAsync(string codigo)
        => Task.FromResult(Permisos.FirstOrDefault(p => p.Codigo == codigo));

    public Task<IEnumerable<Permiso>> GetAllAsync(bool activo = true)
        => Task.FromResult(Permisos.Where(p => p.Activo == activo).AsEnumerable());

    public Task<Permiso> AddAsync(Permiso permiso)
    {
        if (OnAddAsync is not null)
            return OnAddAsync(permiso);

        var nuevoId = Permisos.Count == 0 ? 1 : Permisos.Max(p => p.IdPermiso) + 1;
        var nuevo = Permiso.Reconstruir(
            nuevoId,
            permiso.Codigo,
            permiso.Nombre,
            permiso.Descripcion,
            permiso.Activo,
            permiso.FechaCreacion,
            permiso.UsuarioCreacion);

        Permisos.Add(nuevo);
        return Task.FromResult(nuevo);
    }

    public Task<Permiso> UpdateAsync(Permiso permiso)
    {
        if (OnUpdateAsync is not null)
            return OnUpdateAsync(permiso);

        var existente = Permisos.FirstOrDefault(p => p.IdPermiso == permiso.IdPermiso)
            ?? throw new InvalidOperationException($"Permiso con Id {permiso.IdPermiso} no encontrado.");

        Permisos.Remove(existente);
        Permisos.Add(permiso);
        return Task.FromResult(permiso);
    }

    public Task DeleteAsync(int idPermiso)
    {
        if (OnDeleteAsync is not null)
            return OnDeleteAsync(idPermiso);

        var existente = Permisos.FirstOrDefault(p => p.IdPermiso == idPermiso);
        if (existente is not null)
            Permisos.Remove(existente);

        return Task.CompletedTask;
    }

    public void Add(int idPermiso, string codigo, string nombre, string descripcion = "", bool activo = true)
        => Permisos.Add(Permiso.Reconstruir(idPermiso, codigo, nombre, descripcion, activo));
}
