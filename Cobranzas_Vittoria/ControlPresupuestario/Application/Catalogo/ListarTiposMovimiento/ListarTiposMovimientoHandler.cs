using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarTiposMovimiento;

public sealed class ListarTiposMovimientoHandler
{
    private readonly ICatalogoControlPresupuestarioRepository _repository;

    public ListarTiposMovimientoHandler(ICatalogoControlPresupuestarioRepository repository) => _repository = repository;

    public Task<IReadOnlyList<TipoMovimientoPresupuestal>> HandleAsync(ListarTiposMovimientoQuery query) => _repository.ListarTiposMovimientoAsync(query.Activo);
}
