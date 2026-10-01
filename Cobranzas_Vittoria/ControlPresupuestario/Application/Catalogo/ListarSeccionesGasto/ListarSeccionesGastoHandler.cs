using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarSeccionesGasto;

public sealed class ListarSeccionesGastoHandler
{
    private readonly ICatalogoControlPresupuestarioRepository _repository;

    public ListarSeccionesGastoHandler(ICatalogoControlPresupuestarioRepository repository) => _repository = repository;

    public Task<IReadOnlyList<SeccionGasto>> HandleAsync(ListarSeccionesGastoQuery query) => _repository.ListarSeccionesGastoAsync(query.Activo);
}
