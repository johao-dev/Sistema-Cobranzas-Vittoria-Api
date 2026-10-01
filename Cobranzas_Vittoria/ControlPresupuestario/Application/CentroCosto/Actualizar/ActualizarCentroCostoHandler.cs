using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Actualizar;

public sealed class ActualizarCentroCostoHandler
{
    private readonly ICentroCostoRepository _repository;
    private readonly ILogger<ActualizarCentroCostoHandler> _logger;

    public ActualizarCentroCostoHandler(ICentroCostoRepository repository, ILogger<ActualizarCentroCostoHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CentroCostoResult> HandleAsync(ActualizarCentroCostoCommand command)
    {
        CentroCostoValidator.ValidarId(command.IdCentroCosto);
        var centro = await _repository.ObtenerAsync(command.IdCentroCosto)
            ?? throw new CentroCostoNoEncontradoException(command.IdCentroCosto);
        centro.Actualizar(command.Nombre, command.Activo, command.Descripcion, command.IdProyecto,
            command.Codigo, command.IdTipoCentroCosto);
        await _repository.ActualizarAsync(centro);
        _logger.LogInformation("Centro de costo actualizado: IdCentroCosto={Id}", command.IdCentroCosto);
        var actualizado = await _repository.ObtenerAsync(command.IdCentroCosto)
            ?? throw new CentroCostoNoEncontradoException(command.IdCentroCosto);
        return CentroCostoResult.Desde(actualizado);
    }
}
