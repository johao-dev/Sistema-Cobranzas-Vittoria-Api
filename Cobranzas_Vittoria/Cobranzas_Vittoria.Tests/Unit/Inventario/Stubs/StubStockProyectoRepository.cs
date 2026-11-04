using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Domain.Persistence;

namespace Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;

internal sealed class StubStockProyectoRepository : IStockProyectoRepository
{
    public Task<IReadOnlyList<StockProyecto>> ListarStockAsync(FiltroStockProyecto filtro, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<StockProyecto>>(Array.Empty<StockProyecto>());

    public Task<IReadOnlyList<TransferenciaProyecto>> ListarTransferenciasAsync(FiltroTransferenciaProyecto filtro, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<TransferenciaProyecto>> RegistrarTransferenciaAsync(RegistroTransferenciaProyecto registro, CancellationToken ct = default)
        => throw new NotSupportedException();
}
