using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Categorias;

public sealed class ListarCategoriasHandler
{
    private readonly IGastoDirectoRepository _repository;

    public ListarCategoriasHandler(IGastoDirectoRepository repository) => _repository = repository;

    public Task<IReadOnlyList<CategoriaGastoDirecto>> HandleAsync(ListarCategoriasQuery _)
        => _repository.ListarCategoriasAsync();
}
