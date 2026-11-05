using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;


/// <summary>Puerto de lectura del ledger y de registro de ajustes manuales.</summary>
public interface IMovimientoPresupuestalRepository
{
    Task<IReadOnlyList<MovimientoPresupuestal>> ListarPorDetalleAsync(int idPresupuestoDetalle);
    /// <summary>Ledger APROBADO/HISTORICO de una partida presente en la línea base vigente.</summary>
    Task<IReadOnlyList<MovimientoPresupuestal>> ListarConsolidadosAsync(int idPresupuesto, int idCatalogoPartida);
    Task<MovimientoPresupuestal?> ObtenerAsync(long idMovimientoPresupuestal);
    Task<MovimientoPresupuestal?> RegistrarAjusteAsync(AjustePresupuestal ajuste);
}
