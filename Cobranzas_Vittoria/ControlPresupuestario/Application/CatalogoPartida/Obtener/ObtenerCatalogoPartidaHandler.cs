using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Obtener;

public sealed class ObtenerCatalogoPartidaHandler
{
    private readonly ICatalogoPartidaRepository _repository;

    public ObtenerCatalogoPartidaHandler(ICatalogoPartidaRepository repository) => _repository = repository;

    public async Task<CatalogoPartidaResult> HandleAsync(ObtenerCatalogoPartidaQuery query)
    {
        CatalogoPartidaValidator.ValidarId(query.IdCatalogoPartida);
        var partida = await _repository.ObtenerAsync(query.IdCatalogoPartida)
            ?? throw new CatalogoPartidaNoEncontradoException(query.IdCatalogoPartida);
        return CatalogoPartidaResult.Desde(partida);
    }
}
