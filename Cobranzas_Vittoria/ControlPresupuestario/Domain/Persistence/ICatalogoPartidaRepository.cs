using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;


/// <summary>Filtros del catálogo de partidas. SoloRaices e IdPartidaPadre son excluyentes.</summary>
public sealed record FiltroPartidas(bool? Activo = null, int? IdTipoPartida = null, int? IdPartidaPadre = null,
    bool SoloRaices = false, bool? EsHoja = null, string? Busqueda = null, int? IdSeccionGasto = null);

/// <summary>Puerto de persistencia del catálogo de partidas.</summary>
public interface ICatalogoPartidaRepository
{
    Task<IReadOnlyList<CatalogoPartida>> ListarAsync(FiltroPartidas filtro);
    Task<CatalogoPartida?> ObtenerAsync(int idCatalogoPartida);
    Task<int> CrearAsync(CatalogoPartida partida);
    Task ActualizarAsync(CatalogoPartida partida);
}
