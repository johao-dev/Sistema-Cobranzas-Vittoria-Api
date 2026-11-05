using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.PresupuestoVsComprometido;

/// <summary>Presupuesto frente a compromiso pendiente por partida y versión (vw_PresupuestoVsComprometido).</summary>
public sealed class PresupuestoVsComprometidoHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public PresupuestoVsComprometidoHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<PresupuestoVsComprometidoFila>> HandleAsync(ConsultaPresupuestariaQuery query)
    {
        if (!query.Consolidado)
            return await _repository.PresupuestoVsComprometidoAsync(query.AFiltro());

        IReadOnlyList<SaldoPartidaFila> filas = await ConsultaConsolidada.LeerAsync(_repository, query);
        return filas.Select(ConsultaConsolidada.AComprometido).ToList();
    }
}
