using Cobranzas_Vittoria.Inventario.Domain.Model;

namespace Cobranzas_Vittoria.Inventario.Application.StockProyecto;

public interface IStockProyectoService
{
    Task<IReadOnlyList<Domain.Model.StockProyecto>> ListarStockAsync(FiltroStockProyecto filtro, CancellationToken ct = default);
    Task<IReadOnlyList<TransferenciaProyecto>> ListarTransferenciasAsync(FiltroTransferenciaProyecto filtro, CancellationToken ct = default);
    Task<IReadOnlyList<TransferenciaProyecto>> ObtenerTransferenciaAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<TransferenciaProyecto>> RegistrarTransferenciaAsync(RegistroTransferenciaProyecto registro, CancellationToken ct = default);
}
