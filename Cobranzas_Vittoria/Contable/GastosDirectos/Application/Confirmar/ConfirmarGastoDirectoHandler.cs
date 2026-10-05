using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Confirmar;

/// <summary>Confirmar ejecuta el gasto contra el presupuesto; el SP bloquea si la partida quedaría excedida.</summary>
public sealed class ConfirmarGastoDirectoHandler
{
    private readonly IGastoDirectoRepository _repository;
    private readonly ILogger<ConfirmarGastoDirectoHandler> _logger;

    public ConfirmarGastoDirectoHandler(IGastoDirectoRepository repository, ILogger<ConfirmarGastoDirectoHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task HandleAsync(ConfirmarGastoDirectoCommand command)
    {
        GastoDirectoValidator.ValidarId(command.IdGastoDirecto);
        await _repository.ConfirmarAsync(command.IdGastoDirecto);
        _logger.LogInformation("Gasto directo confirmado: IdGastoDirecto={Id}", command.IdGastoDirecto);
    }
}
