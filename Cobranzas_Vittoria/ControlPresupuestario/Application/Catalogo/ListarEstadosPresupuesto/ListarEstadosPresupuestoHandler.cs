using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarEstadosPresupuesto;

public sealed class ListarEstadosPresupuestoHandler
{
    private readonly ICatalogoControlPresupuestarioRepository _repository;

    public ListarEstadosPresupuestoHandler(ICatalogoControlPresupuestarioRepository repository) => _repository = repository;

    public Task<IReadOnlyList<EstadoPresupuesto>> HandleAsync(ListarEstadosPresupuestoQuery query) => _repository.ListarEstadosPresupuestoAsync(query.Activo);
}
