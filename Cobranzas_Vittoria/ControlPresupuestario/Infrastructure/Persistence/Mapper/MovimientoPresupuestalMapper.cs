using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;

public static class MovimientoPresupuestalMapper
{
    public static MovimientoPresupuestal ToDomain(MovimientoPresupuestalEntity e) => MovimientoPresupuestal.Reconstruir(
        e.IdMovimientoPresupuestal, e.IdPresupuestoDetalle, e.IdTipoMovimientoPresupuestal, e.TipoMovimiento ?? string.Empty,
        e.NombreTipoMovimiento, e.ClaveEvento, e.Origen, e.IdOrigen, e.Afectacion, e.Direccion, e.Fecha, e.Monto,
        e.Observacion, e.IdPresupuesto, e.IdPresupuestoVersion, e.NumeroVersion, e.EstadoPresupuesto, e.IdCatalogoPartida,
        e.CodigoPartida, e.NombrePartida, e.CodigoPresupuesto, e.IdMoneda, e.CodigoMoneda, e.SimboloMoneda, e.EsNuevo);
}
