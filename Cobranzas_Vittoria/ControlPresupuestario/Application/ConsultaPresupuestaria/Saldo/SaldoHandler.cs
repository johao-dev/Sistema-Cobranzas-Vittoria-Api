using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Saldo;

/// <summary>Saldo disponible por partida y versión (vw_Saldo).</summary>
public sealed class SaldoHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public SaldoHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<SaldoPartidaFila>> HandleAsync(ConsultaPresupuestariaQuery query)
    {
        if (!query.Consolidado)
            return await _repository.SaldoAsync(query.AFiltro());

        IReadOnlyList<SaldoPartidaFila> filas = await ConsultaConsolidada.LeerAsync(_repository, query);
        return filas;
    }
}
