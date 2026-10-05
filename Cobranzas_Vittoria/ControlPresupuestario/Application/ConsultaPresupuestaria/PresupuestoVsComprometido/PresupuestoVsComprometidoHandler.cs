using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.PresupuestoVsComprometido;

/// <summary>Presupuesto frente a compromiso pendiente por partida y versión (vw_PresupuestoVsComprometido).</summary>
public sealed class PresupuestoVsComprometidoHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public PresupuestoVsComprometidoHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public Task<IReadOnlyList<PresupuestoVsComprometidoFila>> HandleAsync(ConsultaPresupuestariaQuery query) => _repository.PresupuestoVsComprometidoAsync(query.AFiltro());
}
