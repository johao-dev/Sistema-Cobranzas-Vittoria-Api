using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.GastosPorCentroCosto;

/// <summary>Totales por centro de costo y versión (vw_GastosPorCentroCosto).</summary>
public sealed class GastosPorCentroCostoHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public GastosPorCentroCostoHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public Task<IReadOnlyList<GastoPorCentroCostoFila>> HandleAsync(ConsultaPresupuestariaQuery query) => _repository.GastosPorCentroCostoAsync(query.AFiltro());
}
