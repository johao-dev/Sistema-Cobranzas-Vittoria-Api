using Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal;
using Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal.ListarConsolidados;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.MovimientoPresupuestal;

/// <summary>Movimiento original con su versión de origen y los efectos que concilian el consolidado.</summary>
public sealed record MovimientoConsolidadoResponse(long IdMovimientoPresupuestal, int IdPresupuestoDetalle, string Tipo,
    string? NombreTipo, string? Origen, int IdOrigen, string? ClaveEvento, DateTime Fecha, decimal Monto,
    string? Afectacion, string? Direccion, string? Observacion, int IdPresupuesto, int IdPresupuestoVersion,
    int NumeroVersion, string? EstadoPresupuesto, int IdCatalogoPartida, string? CodigoPartida, string? Partida,
    string? CodigoPresupuesto, int IdMoneda, string? Moneda, string? SimboloMoneda, bool EsNuevo,
    int IdTipoMovimientoPresupuestal, decimal EfectoComprometido, decimal EfectoEjecutado)
{
    public static MovimientoConsolidadoResponse Desde(MovimientoConsolidadoResult resultado)
    {
        MovimientoPresupuestalResult r = resultado.Movimiento;
        return new(r.IdMovimientoPresupuestal,
            r.IdPresupuestoDetalle, r.Tipo, r.NombreTipo, r.Origen, r.IdOrigen, r.ClaveEvento, r.Fecha, r.Monto, r.Afectacion,
            r.Direccion, r.Observacion, r.IdPresupuesto, r.IdPresupuestoVersion, r.NumeroVersion, r.EstadoPresupuesto,
            r.IdCatalogoPartida, r.CodigoPartida, r.NombrePartida, r.CodigoPresupuesto, r.IdMoneda, r.CodigoMoneda,
            r.SimboloMoneda, r.EsNuevo, r.IdTipoMovimientoPresupuestal, resultado.EfectoComprometido, resultado.EfectoEjecutado);
    }
}
