using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarMonedas;

public sealed class ListarMonedasHandler
{
    private readonly ICatalogoControlPresupuestarioRepository _repository;

    public ListarMonedasHandler(ICatalogoControlPresupuestarioRepository repository) => _repository = repository;

    public Task<IReadOnlyList<Moneda>> HandleAsync(ListarMonedasQuery query) => _repository.ListarMonedasAsync(query.Activo);
}
