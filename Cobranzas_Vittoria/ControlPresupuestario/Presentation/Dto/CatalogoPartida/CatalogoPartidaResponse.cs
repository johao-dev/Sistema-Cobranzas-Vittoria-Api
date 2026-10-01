using Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.CatalogoPartida;

public sealed record CatalogoPartidaResponse(int IdCatalogoPartida, string Codigo, string Nombre, string? Descripcion,
    int? IdPartidaPadre, string? CodigoPartidaPadre, string? NombrePartidaPadre, int Nivel, int IdTipoPartida,
    string? CodigoTipoPartida, string? NombreTipoPartida, bool Activo, bool EsHoja, int? IdSeccionGasto,
    string? CodigoSeccionGasto, string? NombreSeccionGasto, DateTime? FechaCreacion, DateTime? FechaActualizacion)
{
    public static CatalogoPartidaResponse Desde(CatalogoPartidaResult r) => new(r.IdCatalogoPartida, r.Codigo, r.Nombre,
        r.Descripcion, r.IdPartidaPadre, r.CodigoPartidaPadre, r.NombrePartidaPadre, r.Nivel, r.IdTipoPartida,
        r.CodigoTipoPartida, r.NombreTipoPartida, r.Activo, r.EsHoja, r.IdSeccionGasto, r.CodigoSeccionGasto,
        r.NombreSeccionGasto, r.FechaCreacion, r.FechaActualizacion);
}
