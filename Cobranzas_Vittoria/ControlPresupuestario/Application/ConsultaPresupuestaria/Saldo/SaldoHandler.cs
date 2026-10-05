using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Saldo;

/// <summary>Saldo disponible por partida y versión (vw_Saldo).</summary>
public sealed class SaldoHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public SaldoHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public Task<IReadOnlyList<SaldoPartidaFila>> HandleAsync(ConsultaPresupuestariaQuery query) => _repository.SaldoAsync(query.AFiltro());
}
