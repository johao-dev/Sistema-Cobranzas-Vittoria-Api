using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;

/// <summary>
/// Stub manual de <see cref="IPermisoRepository"/> con colecciones mutables.
/// Sigue la convencion del proyecto de no usar Moq/NSubstitute.
///
/// Cada test puede armar su escenario agregando permisos a <see cref="Permisos"/>
/// o sobreescribiendo el callback <see cref="OnUpdateAsync"/>.
/// </summary>
public sealed class StubPermisoRepository : IPermisoRepository
{
    public List<Permiso> Permisos { get; } = new();

    /// <summary>Permite simular fallos o comportamiento custom en <see cref="UpdateAsync"/>.</summary>
    public Func<Permiso, Task<Permiso>>? OnUpdateAsync { get; set; }

    public Func<Permiso, Task>? OnEnableAsync { get; set; }
    public Func<Permiso, Task>? OnDisableAsync { get; set; }
    public List<Permiso> PermisosHabilitados { get; } = new();
    public List<Permiso> PermisosDeshabilitados { get; } = new();
    public List<int> IdsConsultados { get; } = new();

    public Task<Permiso?> GetByIdAsync(int idPermiso)
    {
        IdsConsultados.Add(idPermiso);
        return Task.FromResult(Permisos.FirstOrDefault(p => p.IdPermiso == idPermiso));
    }

    public Task<IEnumerable<Permiso>> GetAllAsync(bool activo = true)
        => Task.FromResult(Permisos.Where(p => p.Activo == activo).AsEnumerable());

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

    public void Add(int idPermiso, string codigo, string nombre, string descripcion = "", bool activo = true)
        => Permisos.Add(Permiso.Reconstruir(idPermiso, codigo, nombre, descripcion, activo));

    public Task EnableAsync(Permiso permiso)
    {
        PermisosHabilitados.Add(permiso);
        return OnEnableAsync?.Invoke(permiso) ?? Task.CompletedTask;
    }

    public Task DisableAsync(Permiso permiso)
    {
        PermisosDeshabilitados.Add(permiso);
        return OnDisableAsync?.Invoke(permiso) ?? Task.CompletedTask;
    }
}
