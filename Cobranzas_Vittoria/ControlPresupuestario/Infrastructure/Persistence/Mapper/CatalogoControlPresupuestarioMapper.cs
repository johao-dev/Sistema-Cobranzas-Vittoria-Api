using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;

public static class CatalogoControlPresupuestarioMapper
{
    public static EstadoPresupuesto ToDomain(EstadoPresupuestoEntity e)
        => new(e.IdEstadoPresupuesto, e.Codigo, e.Nombre, e.Descripcion, e.Activo);

    public static TipoCentroCosto ToDomain(TipoCentroCostoEntity e)
        => new(e.IdTipoCentroCosto, e.Codigo, e.Nombre, e.Descripcion, e.Activo);

    public static TipoPartida ToDomain(TipoPartidaEntity e)
        => new(e.IdTipoPartida, e.Codigo, e.Nombre, e.Descripcion, e.Activo);

    public static TipoMovimientoPresupuestal ToDomain(TipoMovimientoPresupuestalEntity e)
        => new(e.IdTipoMovimientoPresupuestal, e.Codigo, e.Nombre, e.Descripcion, e.Activo);

    public static Moneda ToDomain(MonedaEntity e) => new(e.IdMoneda, e.Codigo, e.Nombre, e.Simbolo, e.Activo);

    public static SeccionGasto ToDomain(SeccionGastoEntity e)
        => new(e.IdSeccionGasto, e.Codigo, e.Nombre, e.Descripcion, e.Orden, e.Activo, e.CodigosTipoCentroCosto);
}
