using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;


/// <summary>Puerto de los catálogos auxiliares del módulo.</summary>
public interface ICatalogoControlPresupuestarioRepository
{
    Task<IReadOnlyList<EstadoPresupuesto>> ListarEstadosPresupuestoAsync(bool? activo);
    Task<IReadOnlyList<TipoCentroCosto>> ListarTiposCentroCostoAsync(bool? activo);
    Task<IReadOnlyList<TipoPartida>> ListarTiposPartidaAsync(bool? activo);
    Task<IReadOnlyList<TipoMovimientoPresupuestal>> ListarTiposMovimientoAsync(bool? activo);
    Task<IReadOnlyList<Moneda>> ListarMonedasAsync(bool? activo);
    Task<IReadOnlyList<SeccionGasto>> ListarSeccionesGastoAsync(bool? activo);
}
