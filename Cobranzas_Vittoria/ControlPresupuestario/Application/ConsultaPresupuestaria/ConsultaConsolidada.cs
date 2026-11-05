using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria;

/// <summary>
/// Lectura común del consolidado y proyecciones de los reportes. La vista vigente es la única
/// fuente de acumulación histórica; aquí solo se proyectan o agrupan sus importes netos.
/// </summary>
internal static class ConsultaConsolidada
{
    public static async Task<IReadOnlyList<SaldoPartidaFila>> LeerAsync(
        IConsultaPresupuestariaRepository repository, ConsultaPresupuestariaQuery query)
    {
        FiltroConsultaPresupuestaria filtro = query.AFiltro();
        IReadOnlyList<SaldoPartidaFila> filas = await repository.VigenteAsync(filtro);

        if (filas.Select(f => f.IdMoneda).Distinct().Skip(1).Any())
            throw new ValidacionPresupuestariaException("MONEDAS_MIXTAS",
                "La consulta consolidada contiene distintas monedas: indica idMoneda o un presupuesto.");

        return filas;
    }

    public static PresupuestoResumenFila AResumen(SaldoPartidaFila f) => new()
    {
        IdCentroCosto = f.IdCentroCosto,
        CodigoCentroCosto = f.CodigoCentroCosto,
        NombreCentroCosto = f.NombreCentroCosto,
        IdPresupuesto = f.IdPresupuesto,
        CodigoPresupuesto = f.CodigoPresupuesto,
        NombrePresupuesto = f.NombrePresupuesto,
        IdMoneda = f.IdMoneda,
        CodigoMoneda = f.CodigoMoneda,
        SimboloMoneda = f.SimboloMoneda,
        IdPresupuestoVersion = f.IdPresupuestoVersion,
        NumeroVersion = f.NumeroVersion,
        EstadoPresupuesto = f.EstadoPresupuesto,
        IdPresupuestoDetalle = f.IdPresupuestoDetalle,
        IdCatalogoPartida = f.IdCatalogoPartida,
        CodigoPartida = f.CodigoPartida,
        NombrePartida = f.NombrePartida,
        Nivel = f.Nivel,
        MontoPresupuestado = f.MontoPresupuestado,
        MontoComprometido = f.MontoComprometido,
        MontoEjecutado = f.MontoEjecutado,
        SaldoDisponible = f.SaldoDisponible,
    };

    public static PresupuestoVsComprometidoFila AComprometido(SaldoPartidaFila f) => new()
    {
        IdCentroCosto = f.IdCentroCosto,
        CodigoCentroCosto = f.CodigoCentroCosto,
        NombreCentroCosto = f.NombreCentroCosto,
        IdPresupuesto = f.IdPresupuesto,
        CodigoPresupuesto = f.CodigoPresupuesto,
        NombrePresupuesto = f.NombrePresupuesto,
        IdMoneda = f.IdMoneda,
        CodigoMoneda = f.CodigoMoneda,
        SimboloMoneda = f.SimboloMoneda,
        IdPresupuestoVersion = f.IdPresupuestoVersion,
        NumeroVersion = f.NumeroVersion,
        EstadoPresupuesto = f.EstadoPresupuesto,
        IdPresupuestoDetalle = f.IdPresupuestoDetalle,
        IdCatalogoPartida = f.IdCatalogoPartida,
        CodigoPartida = f.CodigoPartida,
        NombrePartida = f.NombrePartida,
        Nivel = f.Nivel,
        MontoPresupuestado = f.MontoPresupuestado,
        MontoComprometido = f.MontoComprometido,
        Diferencia = f.MontoPresupuestado - f.MontoComprometido,
        PorcentajeComprometido = Porcentaje(f.MontoComprometido, f.MontoPresupuestado),
    };

    public static PresupuestoVsEjecutadoFila AEjecutado(SaldoPartidaFila f) => new()
    {
        IdCentroCosto = f.IdCentroCosto,
        CodigoCentroCosto = f.CodigoCentroCosto,
        NombreCentroCosto = f.NombreCentroCosto,
        IdPresupuesto = f.IdPresupuesto,
        CodigoPresupuesto = f.CodigoPresupuesto,
        NombrePresupuesto = f.NombrePresupuesto,
        IdMoneda = f.IdMoneda,
        CodigoMoneda = f.CodigoMoneda,
        SimboloMoneda = f.SimboloMoneda,
        IdPresupuestoVersion = f.IdPresupuestoVersion,
        NumeroVersion = f.NumeroVersion,
        EstadoPresupuesto = f.EstadoPresupuesto,
        IdPresupuestoDetalle = f.IdPresupuestoDetalle,
        IdCatalogoPartida = f.IdCatalogoPartida,
        CodigoPartida = f.CodigoPartida,
        NombrePartida = f.NombrePartida,
        Nivel = f.Nivel,
        MontoPresupuestado = f.MontoPresupuestado,
        MontoEjecutado = f.MontoEjecutado,
        Diferencia = f.MontoPresupuestado - f.MontoEjecutado,
        PorcentajeEjecutado = Porcentaje(f.MontoEjecutado, f.MontoPresupuestado),
    };

    public static GastoPorPartidaFila AGastoPorPartida(SaldoPartidaFila f) => new()
    {
        IdCentroCosto = f.IdCentroCosto,
        CodigoCentroCosto = f.CodigoCentroCosto,
        NombreCentroCosto = f.NombreCentroCosto,
        IdPresupuesto = f.IdPresupuesto,
        CodigoPresupuesto = f.CodigoPresupuesto,
        NombrePresupuesto = f.NombrePresupuesto,
        IdMoneda = f.IdMoneda,
        CodigoMoneda = f.CodigoMoneda,
        SimboloMoneda = f.SimboloMoneda,
        IdPresupuestoVersion = f.IdPresupuestoVersion,
        NumeroVersion = f.NumeroVersion,
        EstadoPresupuesto = f.EstadoPresupuesto,
        IdCatalogoPartida = f.IdCatalogoPartida,
        CodigoPartida = f.CodigoPartida,
        NombrePartida = f.NombrePartida,
        Nivel = f.Nivel,
        MontoPresupuestado = f.MontoPresupuestado,
        MontoEjecutado = f.MontoEjecutado,
        Diferencia = f.MontoPresupuestado - f.MontoEjecutado,
        PorcentajeEjecutado = Porcentaje(f.MontoEjecutado, f.MontoPresupuestado),
    };

    public static IReadOnlyList<GastoPorCentroCostoFila> AgruparPorCentro(
        IReadOnlyList<PresupuestoResumenFila> filas)
        => filas.GroupBy(f => (f.IdCentroCosto, f.IdPresupuesto, f.IdPresupuestoVersion))
            .Select(g =>
            {
                PresupuestoResumenFila f = g.First();
                decimal presupuestado = g.Sum(p => p.MontoPresupuestado);
                decimal ejecutado = g.Sum(p => p.MontoEjecutado);
                decimal saldo = g.Sum(p => p.SaldoDisponible);

                return new GastoPorCentroCostoFila
                {
                    IdCentroCosto = f.IdCentroCosto,
                    CodigoCentroCosto = f.CodigoCentroCosto,
                    NombreCentroCosto = f.NombreCentroCosto,
                    IdPresupuesto = f.IdPresupuesto,
                    CodigoPresupuesto = f.CodigoPresupuesto,
                    NombrePresupuesto = f.NombrePresupuesto,
                    IdMoneda = f.IdMoneda,
                    CodigoMoneda = f.CodigoMoneda,
                    SimboloMoneda = f.SimboloMoneda,
                    IdPresupuestoVersion = f.IdPresupuestoVersion,
                    NumeroVersion = f.NumeroVersion,
                    EstadoPresupuesto = f.EstadoPresupuesto,
                    MontoPresupuestado = presupuestado,
                    MontoComprometido = g.Sum(p => p.MontoComprometido),
                    MontoEjecutado = ejecutado,
                    SaldoDisponible = saldo,
                    PorcentajeEjecutado = Porcentaje(ejecutado, presupuestado),
                    Excedido = saldo < 0
                };
            })
            .OrderBy(f => f.CodigoCentroCosto).ThenBy(f => f.CodigoPresupuesto).ThenBy(f => f.NumeroVersion)
            .ToList();

    private static decimal Porcentaje(decimal monto, decimal presupuesto)
        => presupuesto == 0 ? 0m : monto * 100m / presupuesto;
}
