using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto.Listar;

public sealed class ListarPresupuestoHandler
{
    private readonly IPresupuestoRepository _repository;

    public ListarPresupuestoHandler(IPresupuestoRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<PresupuestoResult>> HandleAsync(ListarPresupuestoQuery q)
    {
        Validacion.IdOpcional(q.IdCentroCosto, "IdCentroCosto");
        Validacion.IdOpcional(q.IdMoneda, "IdMoneda");
        var presupuestos = await _repository.ListarAsync(q.Activo, q.IdCentroCosto, q.IdMoneda, Validacion.Texto(q.Busqueda));
        return presupuestos.Select(PresupuestoResult.Desde).ToList();
    }
}
