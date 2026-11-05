using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.PresupuestoVsEjecutado;

/// <summary>Presupuesto frente a ejecución neta por partida y versión (vw_PresupuestoVsEjecutado).</summary>
public sealed class PresupuestoVsEjecutadoHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public PresupuestoVsEjecutadoHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<PresupuestoVsEjecutadoFila>> HandleAsync(ConsultaPresupuestariaQuery query)
    {
        if (!query.Consolidado)
            return await _repository.PresupuestoVsEjecutadoAsync(query.AFiltro());

        IReadOnlyList<SaldoPartidaFila> filas = await ConsultaConsolidada.LeerAsync(_repository, query);
        return filas.Select(ConsultaConsolidada.AEjecutado).ToList();
    }
}
