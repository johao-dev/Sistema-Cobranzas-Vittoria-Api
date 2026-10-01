using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Vigente;

/// <summary>Línea base aprobada frente al ledger neto acumulado (vw_ControlPresupuestarioVigente).</summary>
public sealed class VigenteHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public VigenteHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public Task<IReadOnlyList<SaldoPartidaFila>> HandleAsync(ConsultaPresupuestariaQuery query) => _repository.VigenteAsync(query.AFiltro());
}
