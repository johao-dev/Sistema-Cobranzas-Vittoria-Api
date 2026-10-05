using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;

public static class PresupuestoVersionMapper
{
    public static PresupuestoVersion ToDomain(PresupuestoVersionEntity e) => PresupuestoVersion.Reconstruir(
        e.IdPresupuestoVersion, e.IdPresupuesto, e.NumeroVersion, e.IdEstadoPresupuesto, e.EstadoPresupuesto ?? string.Empty,
        e.NombreEstadoPresupuesto, e.Descripcion, e.MotivoCambio, e.MotivoAnulacion, e.FechaCreacion, e.FechaAprobacion,
        e.FechaAnulacion, e.UsuarioCreacion, e.UsuarioAprobacion, e.UsuarioAnulacion, e.CodigoPresupuesto,
        e.NombrePresupuesto, e.IdCentroCosto, e.IdMoneda, e.CodigoMoneda, e.SimboloMoneda);

    public static VersionCreada ToDomain(VersionCreadaEntity e) => new(e.IdPresupuesto, e.IdPresupuestoVersion,
        e.NumeroVersion, e.EstadoPresupuesto ?? string.Empty, e.IdPresupuestoVersionBase, e.CantidadDetallesCopiados);
}
