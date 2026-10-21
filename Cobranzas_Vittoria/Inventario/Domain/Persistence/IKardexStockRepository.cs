using Cobranzas_Vittoria.Inventario.Domain.Model;

namespace Cobranzas_Vittoria.Inventario.Domain.Persistence;

/// <summary>Contrato de persistencia del Kardex manual con errores de negocio tipados.</summary>
public interface IKardexStockRepository
{
    /// <summary>
    /// Lista el stock actual consolidado con joins a maestra.
    /// </summary>
    /// <param name="filtro">Filtro de busqueda (todos los campos son opcionales).</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<StockKardex>> ListarAsync(
        FiltroStockKardex filtro,
        CancellationToken ct = default);
}
