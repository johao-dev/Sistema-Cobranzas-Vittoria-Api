using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;

public static class PresupuestoDetalleMapper
{
    public static PresupuestoDetalle ToDomain(PresupuestoDetalleEntity e) => PresupuestoDetalle.Reconstruir(
        e.IdPresupuestoDetalle, e.IdPresupuestoVersion, e.IdCatalogoPartida, e.CodigoPartida, e.NombrePartida,
        e.IdPartidaPadre, e.Nivel, e.IdTipoPartida, e.CodigoTipoPartida, e.NombreTipoPartida, e.PartidaActiva, e.EsHoja,
        e.MontoPresupuestado, e.Observacion, e.FechaCreacion, e.FechaModificacion);

    public static ResultadoCargaLote ToDomain(CargaLoteEntity e) => new(e.IdPresupuestoVersion, e.Agregados,
        e.Actualizados, e.Eliminados, e.PartidasEnVersion, e.MontoTotal);
}
