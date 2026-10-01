using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;

public static class CatalogoPartidaMapper
{
    public static CatalogoPartida ToDomain(CatalogoPartidaEntity e) => CatalogoPartida.Reconstruir(
        e.IdCatalogoPartida, e.Codigo, e.Nombre, e.Descripcion, e.IdPartidaPadre, e.CodigoPartidaPadre,
        e.NombrePartidaPadre, e.Nivel, e.IdTipoPartida, e.CodigoTipoPartida, e.NombreTipoPartida, e.Activo, e.EsHoja,
        e.IdSeccionGasto, e.CodigoSeccionGasto, e.NombreSeccionGasto, e.FechaCreacion, e.FechaActualizacion);
}
