using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal.ListarPorDetalle;

public sealed class ListarMovimientosPorDetalleHandler
{
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;
    private readonly IMovimientoPresupuestalRepository _movimientos;

    public ListarMovimientosPorDetalleHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles,
        IMovimientoPresupuestalRepository movimientos)
    {
        _versiones = versiones;
        _detalles = detalles;
        _movimientos = movimientos;
    }

    public async Task<IReadOnlyList<MovimientoPresupuestalResult>> HandleAsync(ListarMovimientosPorDetalleQuery q)
    {
        PresupuestoDetalleValidator.ValidarIds(q.IdPresupuesto, q.IdPresupuestoVersion, q.IdPresupuestoDetalle);
        await _versiones.ObtenerDelPresupuestoAsync(q.IdPresupuesto, q.IdPresupuestoVersion);
        await _detalles.ObtenerDeLaVersionAsync(q.IdPresupuestoVersion, q.IdPresupuestoDetalle);
        return (await _movimientos.ListarPorDetalleAsync(q.IdPresupuestoDetalle)).Select(MovimientoPresupuestalResult.Desde).ToList();
    }
}
