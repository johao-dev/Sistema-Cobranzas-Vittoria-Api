using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;


/// <summary>Puerto de persistencia de versiones de presupuesto.</summary>
public interface IPresupuestoVersionRepository
{
    Task<IReadOnlyList<PresupuestoVersion>> ListarAsync(int idPresupuesto);
    Task<PresupuestoVersion?> ObtenerAsync(int idPresupuestoVersion);
    Task<VersionCreada> CrearNuevaAsync(int idPresupuesto, string? descripcion, string? motivoCambio, string usuario);
    Task AprobarAsync(int idPresupuestoVersion, string usuario);
    Task AnularAsync(int idPresupuestoVersion, string motivo, string usuario);
}
