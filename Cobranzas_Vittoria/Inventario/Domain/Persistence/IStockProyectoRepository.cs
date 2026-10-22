using Cobranzas_Vittoria.Inventario.Domain.Model;

namespace Cobranzas_Vittoria.Inventario.Domain.Persistence;

public interface IStockProyectoRepository
{
    Task<IReadOnlyList<StockProyecto>> ListarStockAsync(FiltroStockProyecto filtro, CancellationToken ct = default);
    Task<IReadOnlyList<TransferenciaProyecto>> ListarTransferenciasAsync(FiltroTransferenciaProyecto filtro, CancellationToken ct = default);
    Task<IReadOnlyList<TransferenciaProyecto>> RegistrarTransferenciaAsync(RegistroTransferenciaProyecto registro, CancellationToken ct = default);
}
