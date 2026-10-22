using Cobranzas_Vittoria.Inventario.Application.Common;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Domain.Persistence;

namespace Cobranzas_Vittoria.Inventario.Application.StockProyecto;

public sealed class StockProyectoService : IStockProyectoService
{
    private readonly IStockProyectoRepository _repository;
    private readonly TransferenciaProyectoValidator _validator;

    public StockProyectoService(IStockProyectoRepository repository, TransferenciaProyectoValidator validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public Task<IReadOnlyList<Domain.Model.StockProyecto>> ListarStockAsync(FiltroStockProyecto filtro, CancellationToken ct = default)
        => _repository.ListarStockAsync(filtro, ct);

    public Task<IReadOnlyList<TransferenciaProyecto>> ListarTransferenciasAsync(FiltroTransferenciaProyecto filtro, CancellationToken ct = default)
        => _repository.ListarTransferenciasAsync(filtro, ct);

    public async Task<IReadOnlyList<TransferenciaProyecto>> ObtenerTransferenciaAsync(int id, CancellationToken ct = default)
    {
        IReadOnlyList<TransferenciaProyecto> filas = await _repository.ListarTransferenciasAsync(new() { IdTransferencia = id }, ct);
        if (filas.Count == 0)
        {
            throw new KeyNotFoundException($"No se encontró la transferencia de proyectos con Id={id}.");
        }

        return filas;
    }

    public Task<IReadOnlyList<TransferenciaProyecto>> RegistrarTransferenciaAsync(RegistroTransferenciaProyecto registro, CancellationToken ct = default)
    {
        _validator.Validar(registro);
        return _repository.RegistrarTransferenciaAsync(registro, ct);
    }
}
