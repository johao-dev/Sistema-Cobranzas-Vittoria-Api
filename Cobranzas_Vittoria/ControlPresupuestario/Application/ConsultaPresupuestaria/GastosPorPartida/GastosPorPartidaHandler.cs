using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.GastosPorPartida;

/// <summary>Ejecución agregada por partida (vw_GastosPorPartida).</summary>
public sealed class GastosPorPartidaHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public GastosPorPartidaHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public Task<IReadOnlyList<GastoPorPartidaFila>> HandleAsync(ConsultaPresupuestariaQuery query) => _repository.GastosPorPartidaAsync(query.AFiltro());
}
