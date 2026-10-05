using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Service;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.PlantillaEstructura;

public sealed record ObtenerPlantillaEstructuraQuery(int IdPresupuesto, int IdPresupuestoVersion);

/// <summary>Fila de la plantilla jerárquica. Monto va vacío en las categorías; Subtotal es informativo y solo va en ellas.</summary>
public sealed record PlantillaEstructuraFila(string Codigo, string Nombre, string? Tipo, string? Seccion, decimal? Monto,
    decimal? Subtotal, string? Observacion);

/// <summary>
/// Plantilla de importación jerárquica de una versión: el árbol del catálogo activo (más las partidas
/// inactivas que la versión ya usa) en preorden, con el monto actual de cada hoja (0 si no lo tiene) y el
/// subtotal de cada categoría. Se puede editar y volver a subir en …/partidas/importar-estructura.
/// </summary>
public sealed class ObtenerPlantillaEstructuraHandler
{
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;
    private readonly ICatalogoPartidaRepository _partidas;

    public ObtenerPlantillaEstructuraHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles,
        ICatalogoPartidaRepository partidas)
    {
        _versiones = versiones;
        _detalles = detalles;
        _partidas = partidas;
    }

    public async Task<IReadOnlyList<PlantillaEstructuraFila>> HandleAsync(ObtenerPlantillaEstructuraQuery query)
    {
        PresupuestoVersionValidator.ValidarIds(query.IdPresupuesto, query.IdPresupuestoVersion);
        await _versiones.ObtenerDelPresupuestoAsync(query.IdPresupuesto, query.IdPresupuestoVersion);
        var detalles = (await _detalles.ListarPorVersionAsync(query.IdPresupuestoVersion)).ToDictionary(d => d.IdCatalogoPartida);
        var partidas = (await _partidas.ListarAsync(new FiltroPartidas()))
            .Where(p => p.Activo || detalles.ContainsKey(p.IdCatalogoPartida))
            .ToList();
        var ids = partidas.Select(p => p.IdCatalogoPartida).ToHashSet();
        var hijas = partidas.ToLookup(p => p.IdPartidaPadre is int padre && ids.Contains(padre) ? padre : (int?)null);
        var orden = Comparer<string>.Create(ArbolPresupuestario.CompararCodigos);

        var filas = new List<PlantillaEstructuraFila>(partidas.Count);
        foreach (var raiz in hijas[null].OrderBy(p => p.Codigo, orden))
            Visitar(raiz);
        return filas;

        decimal Visitar(Domain.Model.CatalogoPartida partida)
        {
            var posicion = filas.Count;
            filas.Add(null!);
            var propias = hijas[partida.IdCatalogoPartida].OrderBy(p => p.Codigo, orden).ToList();
            if (propias.Count == 0)
            {
                detalles.TryGetValue(partida.IdCatalogoPartida, out var d);
                var monto = d?.MontoPresupuestado ?? 0m;
                filas[posicion] = new PlantillaEstructuraFila(partida.Codigo, partida.Nombre, partida.CodigoTipoPartida,
                    partida.CodigoSeccionGasto, monto, null, d?.Observacion);
                return monto;
            }
            var subtotal = propias.Sum(Visitar);
            filas[posicion] = new PlantillaEstructuraFila(partida.Codigo, partida.Nombre, partida.CodigoTipoPartida, null, null,
                subtotal, null);
            return subtotal;
        }
    }
}
