namespace Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal.ListarConsolidados;

/// <summary>Importe original y efectos firmados, según las fórmulas de vw_PresupuestoResumen.</summary>
public sealed record MovimientoConsolidadoResult(MovimientoPresupuestalResult Movimiento,
    decimal EfectoComprometido, decimal EfectoEjecutado)
{
    public static MovimientoConsolidadoResult Desde(Domain.Model.MovimientoPresupuestal movimiento)
    {
        decimal ajuste = movimiento.Direccion switch
        {
            "INCREMENTO" => movimiento.Monto,
            "DECREMENTO" => -movimiento.Monto,
            _ => 0m
        };
        decimal compromiso = movimiento.Tipo switch
        {
            "COMPROMISO" => movimiento.Monto,
            "LIBERACION" => -movimiento.Monto,
            "AJUSTE" when movimiento.Afectacion == "COMPROMISO" => ajuste,
            _ => 0m
        };
        decimal ejecucion = movimiento.Tipo switch
        {
            "EJECUCION" => movimiento.Monto,
            "AJUSTE" when movimiento.Afectacion == "EJECUCION" => ajuste,
            _ => 0m
        };

        return new(MovimientoPresupuestalResult.Desde(movimiento), compromiso, ejecucion);
    }
}
