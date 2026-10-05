namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida;

/// <summary>Partida del catálogo. IdPartidaPadre permite al cliente reconstruir el árbol.</summary>
public sealed record CatalogoPartidaResult(int IdCatalogoPartida, string Codigo, string Nombre, string? Descripcion,
    int? IdPartidaPadre, string? CodigoPartidaPadre, string? NombrePartidaPadre, int Nivel, int IdTipoPartida,
    string? CodigoTipoPartida, string? NombreTipoPartida, bool Activo, bool EsHoja, int? IdSeccionGasto,
    string? CodigoSeccionGasto, string? NombreSeccionGasto, DateTime? FechaCreacion, DateTime? FechaActualizacion)
{
    public static CatalogoPartidaResult Desde(Domain.Model.CatalogoPartida p) => new(p.IdCatalogoPartida, p.Codigo,
        p.Nombre, p.Descripcion, p.IdPartidaPadre, p.CodigoPartidaPadre, p.NombrePartidaPadre, p.Nivel, p.IdTipoPartida,
        p.CodigoTipoPartida, p.NombreTipoPartida, p.Activo, p.EsHoja, p.IdSeccionGasto, p.CodigoSeccionGasto,
        p.NombreSeccionGasto, p.FechaCreacion, p.FechaActualizacion);
}
