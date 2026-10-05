using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarTiposPartida;

public sealed class ListarTiposPartidaHandler
{
    private readonly ICatalogoControlPresupuestarioRepository _repository;

    public ListarTiposPartidaHandler(ICatalogoControlPresupuestarioRepository repository) => _repository = repository;

    public Task<IReadOnlyList<TipoPartida>> HandleAsync(ListarTiposPartidaQuery query) => _repository.ListarTiposPartidaAsync(query.Activo);
}
