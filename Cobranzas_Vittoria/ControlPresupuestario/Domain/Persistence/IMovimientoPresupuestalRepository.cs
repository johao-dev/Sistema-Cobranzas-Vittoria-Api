using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;


/// <summary>Puerto de lectura del ledger y de registro de ajustes manuales.</summary>
public interface IMovimientoPresupuestalRepository
{
    Task<IReadOnlyList<MovimientoPresupuestal>> ListarPorDetalleAsync(int idPresupuestoDetalle);
    Task<MovimientoPresupuestal?> ObtenerAsync(long idMovimientoPresupuestal);
    Task<MovimientoPresupuestal?> RegistrarAjusteAsync(AjustePresupuestal ajuste);
}
