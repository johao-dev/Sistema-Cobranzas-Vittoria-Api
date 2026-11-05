using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Resumen;

/// <summary>
/// Resumen por centro, presupuesto y versión base. Conserva la lectura individual predeterminada;
/// en modo consolidado utiliza la línea base vigente y sus movimientos APROBADO/HISTORICO.
/// </summary>
public sealed class ResumenHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public ResumenHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public async Task<ResumenResult> HandleAsync(ConsultaPresupuestariaQuery query)
    {
        FiltroConsultaPresupuestaria filtro = query.AFiltro();
        IReadOnlyList<PresupuestoResumenFila> filas = query.Consolidado
            ? (await ConsultaConsolidada.LeerAsync(_repository, query)).Select(ConsultaConsolidada.AResumen).ToList()
            : await _repository.ResumenAsync(filtro);

        IReadOnlyList<GastoPorCentroCostoFila> porCentroCosto = ConsultaConsolidada.AgruparPorCentro(filas);

        List<SaldoPartidaFila> partidas = filas
            .Select(f => new SaldoPartidaFila
            {
                IdCentroCosto = f.IdCentroCosto, CodigoCentroCosto = f.CodigoCentroCosto, NombreCentroCosto = f.NombreCentroCosto,
                IdPresupuesto = f.IdPresupuesto, CodigoPresupuesto = f.CodigoPresupuesto, NombrePresupuesto = f.NombrePresupuesto,
                IdMoneda = f.IdMoneda, CodigoMoneda = f.CodigoMoneda, SimboloMoneda = f.SimboloMoneda,
                IdPresupuestoVersion = f.IdPresupuestoVersion, NumeroVersion = f.NumeroVersion,
                EstadoPresupuesto = f.EstadoPresupuesto,
                IdPresupuestoDetalle = f.IdPresupuestoDetalle, IdCatalogoPartida = f.IdCatalogoPartida,
                CodigoPartida = f.CodigoPartida, NombrePartida = f.NombrePartida, Nivel = f.Nivel,
                MontoPresupuestado = f.MontoPresupuestado, MontoComprometido = f.MontoComprometido,
                MontoEjecutado = f.MontoEjecutado, SaldoDisponible = f.SaldoDisponible,
                MontoExcedido = f.SaldoDisponible < 0 ? -f.SaldoDisponible : 0m,
                Excedido = f.SaldoDisponible < 0
            })
            .ToList();
        List<SaldoPartidaFila> excedidas = partidas.Where(p => p.Excedido).OrderByDescending(p => p.MontoExcedido).ToList();

        decimal totalPresupuestado = porCentroCosto.Sum(c => c.MontoPresupuestado);
        decimal totalEjecutado = porCentroCosto.Sum(c => c.MontoEjecutado);
        ResumenTotales totales = new(
            totalPresupuestado,
            porCentroCosto.Sum(c => c.MontoComprometido),
            totalEjecutado,
            porCentroCosto.Sum(c => c.SaldoDisponible),
            totalPresupuestado == 0 ? 0m : decimal.Round(totalEjecutado * 100m / totalPresupuestado, 2, MidpointRounding.AwayFromZero),
            porCentroCosto.Count,
            partidas.Count,
            excedidas.Count);
        return new ResumenResult(totales, porCentroCosto, excedidas);
    }
}
