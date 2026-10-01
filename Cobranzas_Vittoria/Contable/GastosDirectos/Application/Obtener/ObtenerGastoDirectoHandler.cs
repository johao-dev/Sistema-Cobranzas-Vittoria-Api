using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Obtener;

public sealed class ObtenerGastoDirectoHandler
{
    private readonly IGastoDirectoRepository _repository;

    public ObtenerGastoDirectoHandler(IGastoDirectoRepository repository) => _repository = repository;

    public async Task<ObtenerGastoDirectoResult> HandleAsync(ObtenerGastoDirectoQuery query)
    {
        GastoDirectoValidator.ValidarId(query.IdGastoDirecto);
        var (gasto, documentos) = await _repository.ObtenerAsync(query.IdGastoDirecto);
        return gasto is null
            ? throw GastoDirectoNoEncontradoException.Gasto(query.IdGastoDirecto)
            : new ObtenerGastoDirectoResult(gasto, documentos);
    }
}
