using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Resumen;

/// <summary>
/// Resumen presupuestario armado sobre vw_PresupuestoResumen, la vista base: agrega por centro de
/// costo y versión con el mismo criterio que vw_GastosPorCentroCosto y marca excedida la partida cuyo
/// saldo disponible es negativo, como vw_Saldo.
/// </summary>
public sealed class ResumenHandler
{
    private readonly IConsultaPresupuestariaRepository _repository;

    public ResumenHandler(IConsultaPresupuestariaRepository repository) => _repository = repository;

    public async Task<ResumenResult> HandleAsync(ConsultaPresupuestariaQuery query)
    {
        var filtro = query.AFiltro();
        var soloExcedidos = filtro.SoloExcedidos == true;
        var filas = await _repository.ResumenAsync(filtro with { SoloExcedidos = null });

        var porCentroCosto = filas
            .GroupBy(f => (f.IdCentroCosto, f.IdPresupuesto, f.IdPresupuestoVersion))
            .Select(g =>
            {
                var p = g.First();
                var presupuestado = g.Sum(f => f.MontoPresupuestado);
                var ejecutado = g.Sum(f => f.MontoEjecutado);
                var saldo = g.Sum(f => f.SaldoDisponible);
                return new GastoPorCentroCostoFila
                {
                    IdCentroCosto = p.IdCentroCosto, CodigoCentroCosto = p.CodigoCentroCosto, NombreCentroCosto = p.NombreCentroCosto,
                    IdPresupuesto = p.IdPresupuesto, CodigoPresupuesto = p.CodigoPresupuesto, NombrePresupuesto = p.NombrePresupuesto,
                    IdMoneda = p.IdMoneda, CodigoMoneda = p.CodigoMoneda, SimboloMoneda = p.SimboloMoneda,
                    IdPresupuestoVersion = p.IdPresupuestoVersion, NumeroVersion = p.NumeroVersion,
                    EstadoPresupuesto = p.EstadoPresupuesto,
                    MontoPresupuestado = presupuestado,
                    MontoComprometido = g.Sum(f => f.MontoComprometido),
                    MontoEjecutado = ejecutado,
                    SaldoDisponible = saldo,
                    PorcentajeEjecutado = presupuestado == 0 ? 0m : ejecutado * 100m / presupuestado,
                    Excedido = saldo < 0
                };
            })
            .Where(c => !soloExcedidos || c.Excedido)
            .OrderBy(c => c.CodigoCentroCosto).ThenBy(c => c.CodigoPresupuesto).ThenBy(c => c.NumeroVersion)
            .ToList();

        var partidas = filas
            .Where(f => !soloExcedidos || f.SaldoDisponible < 0)
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
        var excedidas = partidas.Where(p => p.Excedido).OrderByDescending(p => p.MontoExcedido).ToList();

        var totalPresupuestado = porCentroCosto.Sum(c => c.MontoPresupuestado);
        var totalEjecutado = porCentroCosto.Sum(c => c.MontoEjecutado);
        var totales = new ResumenTotales(
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
