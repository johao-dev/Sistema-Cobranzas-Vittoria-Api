using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Proveedores;

public sealed class ListarProveedoresHandler
{
    private readonly IGastoDirectoRepository _repository;

    public ListarProveedoresHandler(IGastoDirectoRepository repository) => _repository = repository;

    public Task<IReadOnlyList<ProveedorGastoDirecto>> HandleAsync(ListarProveedoresQuery query)
        => _repository.ListarProveedoresAsync();
}
