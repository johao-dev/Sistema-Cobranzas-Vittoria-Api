using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;

public static class PresupuestoMapper
{
    public static Presupuesto ToDomain(PresupuestoEntity e) => Presupuesto.Reconstruir(
        e.IdPresupuesto, e.Codigo, e.Nombre, e.Descripcion, e.IdCentroCosto, e.CodigoCentroCosto, e.NombreCentroCosto,
        e.IdMoneda, e.CodigoMoneda, e.NombreMoneda, e.SimboloMoneda, e.FechaInicio, e.FechaFin, e.Activo,
        e.FechaCreacion, e.FechaModificacion, e.IdPresupuestoVersionAprobada, e.IdPresupuestoVersionBorrador,
        e.CantidadAprobadas, e.CantidadBorradores, e.EstadoElaboracion);

    public static PresupuestoCreado ToDomain(PresupuestoCreadoEntity e)
        => new(e.IdPresupuesto, e.IdPresupuestoVersion, e.NumeroVersion, e.EstadoPresupuesto ?? string.Empty);
}
