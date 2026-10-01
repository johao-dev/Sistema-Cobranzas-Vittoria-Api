using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;


/// <summary>Puerto de las consultas de reporting: cada método lee una vista existente.</summary>
public interface IConsultaPresupuestariaRepository
{
    Task<IReadOnlyList<PresupuestoResumenFila>> ResumenAsync(FiltroConsultaPresupuestaria filtro);
    Task<IReadOnlyList<PresupuestoVsComprometidoFila>> PresupuestoVsComprometidoAsync(FiltroConsultaPresupuestaria filtro);
    Task<IReadOnlyList<PresupuestoVsEjecutadoFila>> PresupuestoVsEjecutadoAsync(FiltroConsultaPresupuestaria filtro);
    Task<IReadOnlyList<SaldoPartidaFila>> SaldoAsync(FiltroConsultaPresupuestaria filtro);
    Task<IReadOnlyList<GastoPorPartidaFila>> GastosPorPartidaAsync(FiltroConsultaPresupuestaria filtro);
    Task<IReadOnlyList<GastoPorCentroCostoFila>> GastosPorCentroCostoAsync(FiltroConsultaPresupuestaria filtro);
    Task<IReadOnlyList<SaldoPartidaFila>> VigenteAsync(FiltroConsultaPresupuestaria filtro);

    /// <summary>Ejecución neta diaria por partida de los presupuestos indicados (versiones APROBADO/HISTORICO).</summary>
    Task<IReadOnlyList<EjecucionDiariaFila>> EjecucionDiariaAsync(IReadOnlyCollection<int> idsPresupuesto);

    /// <summary>Saldo de una partida concreta de una versión (vw_Saldo).</summary>
    Task<SaldoPartidaFila?> SaldoDetalleAsync(int idPresupuestoDetalle);
}
