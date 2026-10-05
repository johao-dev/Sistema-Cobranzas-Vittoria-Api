using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Crear;

public sealed class CrearCentroCostoHandler
{
    private readonly ICentroCostoRepository _repository;
    private readonly ILogger<CrearCentroCostoHandler> _logger;

    public CrearCentroCostoHandler(ICentroCostoRepository repository, ILogger<CrearCentroCostoHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CentroCostoResult> HandleAsync(CrearCentroCostoCommand command)
    {
        var centro = Domain.Model.CentroCosto.Crear(command.Codigo, command.Nombre, command.IdTipoCentroCosto,
            command.Descripcion, command.IdProyecto);
        var id = await _repository.CrearAsync(centro);
        _logger.LogInformation("Centro de costo creado: IdCentroCosto={Id}, Codigo={Codigo}", id, centro.Codigo);
        var creado = await _repository.ObtenerAsync(id) ?? throw new CentroCostoNoEncontradoException(id);
        return CentroCostoResult.Desde(creado);
    }
}
