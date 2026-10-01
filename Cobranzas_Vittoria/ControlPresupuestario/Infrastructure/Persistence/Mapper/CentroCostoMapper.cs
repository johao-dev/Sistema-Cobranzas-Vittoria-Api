using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;

public static class CentroCostoMapper
{
    public static CentroCosto ToDomain(CentroCostoEntity e) => CentroCosto.Reconstruir(
        e.IdCentroCosto, e.Codigo, e.Nombre, e.Descripcion, e.IdTipoCentroCosto, e.CodigoTipoCentroCosto,
        e.NombreTipoCentroCosto, e.IdProyecto, e.NombreProyecto, e.Activo, e.FechaCreacion, e.FechaModificacion);
}
