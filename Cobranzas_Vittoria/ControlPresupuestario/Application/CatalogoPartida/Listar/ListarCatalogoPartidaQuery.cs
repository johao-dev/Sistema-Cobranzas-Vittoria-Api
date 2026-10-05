namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Listar;

public sealed record ListarCatalogoPartidaQuery(bool? Activo, int? IdTipoPartida, int? IdPartidaPadre, bool SoloRaices,
    bool? EsHoja, string? Busqueda, int? IdSeccionGasto);
