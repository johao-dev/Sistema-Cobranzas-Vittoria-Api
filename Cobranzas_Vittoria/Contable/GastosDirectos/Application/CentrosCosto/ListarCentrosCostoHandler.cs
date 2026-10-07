using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.CentrosCosto;

public sealed class ListarCentrosCostoHandler
{
    private readonly IGastoDirectoRepository _repository;

    public ListarCentrosCostoHandler(IGastoDirectoRepository repository) => _repository = repository;

    public Task<IReadOnlyList<CentroCostoGastoDirecto>> HandleAsync(ListarCentrosCostoQuery query)
        => _repository.ListarCentrosCostoAsync();
}
