using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.GastosPorCentroCosto;

/// <summary>Totales por centro de costo y versión (vw_GastosPorCentroCosto).</summary>
public sealed class GastosPorCentroCostoHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public GastosPorCentroCostoHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<GastoPorCentroCostoFila>> HandleAsync(ConsultaPresupuestariaQuery query)
    {
        if (!query.Consolidado)
            return await _repository.GastosPorCentroCostoAsync(query.AFiltro());

        IReadOnlyList<SaldoPartidaFila> filas = await ConsultaConsolidada.LeerAsync(_repository, query);
        return ConsultaConsolidada.AgruparPorCentro(filas.Select(ConsultaConsolidada.AResumen).ToList());
    }
}
