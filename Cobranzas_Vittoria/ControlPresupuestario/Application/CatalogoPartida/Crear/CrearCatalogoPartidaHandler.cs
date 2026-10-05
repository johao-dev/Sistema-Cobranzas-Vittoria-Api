using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Crear;

public sealed class CrearCatalogoPartidaHandler
{
    private readonly ICatalogoPartidaRepository _repository;
    private readonly ILogger<CrearCatalogoPartidaHandler> _logger;

    public CrearCatalogoPartidaHandler(ICatalogoPartidaRepository repository, ILogger<CrearCatalogoPartidaHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CatalogoPartidaResult> HandleAsync(CrearCatalogoPartidaCommand command)
    {
        var partida = Domain.Model.CatalogoPartida.Crear(command.Codigo, command.Nombre, command.IdTipoPartida,
            command.IdPartidaPadre, command.Descripcion, command.IdSeccionGasto);
        var id = await _repository.CrearAsync(partida);
        _logger.LogInformation("Partida creada: IdCatalogoPartida={Id}, Codigo={Codigo}", id, partida.Codigo);
        var creada = await _repository.ObtenerAsync(id) ?? throw new CatalogoPartidaNoEncontradoException(id);
        return CatalogoPartidaResult.Desde(creada);
    }
}
