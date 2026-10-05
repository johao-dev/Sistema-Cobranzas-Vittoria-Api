using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Actualizar;

public sealed class ActualizarCatalogoPartidaHandler
{
    private readonly ICatalogoPartidaRepository _repository;
    private readonly ILogger<ActualizarCatalogoPartidaHandler> _logger;

    public ActualizarCatalogoPartidaHandler(ICatalogoPartidaRepository repository,
        ILogger<ActualizarCatalogoPartidaHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CatalogoPartidaResult> HandleAsync(ActualizarCatalogoPartidaCommand command)
    {
        CatalogoPartidaValidator.ValidarId(command.IdCatalogoPartida);
        var partida = await _repository.ObtenerAsync(command.IdCatalogoPartida)
            ?? throw new CatalogoPartidaNoEncontradoException(command.IdCatalogoPartida);
        partida.Actualizar(command.Nombre, command.IdTipoPartida, command.Activo, command.IdPartidaPadre,
            command.Descripcion, command.IdSeccionGasto);
        await _repository.ActualizarAsync(partida);
        _logger.LogInformation("Partida actualizada: IdCatalogoPartida={Id}", command.IdCatalogoPartida);
        var actualizada = await _repository.ObtenerAsync(command.IdCatalogoPartida)
            ?? throw new CatalogoPartidaNoEncontradoException(command.IdCatalogoPartida);
        return CatalogoPartidaResult.Desde(actualizada);
    }
}
