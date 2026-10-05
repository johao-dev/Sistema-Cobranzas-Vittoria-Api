using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarTiposCentroCosto;

public sealed class ListarTiposCentroCostoHandler
{
    private readonly ICatalogoControlPresupuestarioRepository _repository;

    public ListarTiposCentroCostoHandler(ICatalogoControlPresupuestarioRepository repository) => _repository = repository;

    public Task<IReadOnlyList<TipoCentroCosto>> HandleAsync(ListarTiposCentroCostoQuery query) => _repository.ListarTiposCentroCostoAsync(query.Activo);
}
