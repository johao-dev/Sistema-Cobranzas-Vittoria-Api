using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.GastosPorPartida;

/// <summary>Ejecución agregada por partida (vw_GastosPorPartida).</summary>
public sealed class GastosPorPartidaHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public GastosPorPartidaHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<GastoPorPartidaFila>> HandleAsync(ConsultaPresupuestariaQuery query)
    {
        if (!query.Consolidado)
            return await _repository.GastosPorPartidaAsync(query.AFiltro());

        IReadOnlyList<SaldoPartidaFila> filas = await ConsultaConsolidada.LeerAsync(_repository, query);
        return filas.Select(ConsultaConsolidada.AGastoPorPartida).ToList();
    }
}
