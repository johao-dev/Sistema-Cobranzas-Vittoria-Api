using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Anular;

/// <summary>Anular un gasto confirmado revierte su ejecución en el ledger (lo hace el SP).</summary>
public sealed class AnularGastoDirectoHandler
{
    private readonly IGastoDirectoRepository _repository;
    private readonly ILogger<AnularGastoDirectoHandler> _logger;

    public AnularGastoDirectoHandler(IGastoDirectoRepository repository, ILogger<AnularGastoDirectoHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task HandleAsync(AnularGastoDirectoCommand command)
    {
        GastoDirectoValidator.ValidarId(command.IdGastoDirecto);
        await _repository.AnularAsync(command.IdGastoDirecto);
        _logger.LogInformation("Gasto directo anulado: IdGastoDirecto={Id}", command.IdGastoDirecto);
    }
}
