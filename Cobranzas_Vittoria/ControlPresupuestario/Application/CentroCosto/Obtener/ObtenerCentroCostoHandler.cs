using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Obtener;

public sealed class ObtenerCentroCostoHandler
{
    private readonly ICentroCostoRepository _repository;

    public ObtenerCentroCostoHandler(ICentroCostoRepository repository) => _repository = repository;

    public async Task<CentroCostoResult> HandleAsync(ObtenerCentroCostoQuery query)
    {
        CentroCostoValidator.ValidarId(query.IdCentroCosto);
        var centro = await _repository.ObtenerAsync(query.IdCentroCosto)
            ?? throw new CentroCostoNoEncontradoException(query.IdCentroCosto);
        return CentroCostoResult.Desde(centro);
    }
}
