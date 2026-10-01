namespace Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal;

public sealed record MovimientoPresupuestalResult(long IdMovimientoPresupuestal, int IdPresupuestoDetalle,
    int IdTipoMovimientoPresupuestal, string Tipo, string? NombreTipo, string? ClaveEvento, string? Origen, int IdOrigen,
    string? Afectacion, string? Direccion, DateTime Fecha, decimal Monto, string? Observacion, int IdPresupuesto,
    int IdPresupuestoVersion, int NumeroVersion, string? EstadoPresupuesto, int IdCatalogoPartida, string? CodigoPartida,
    string? NombrePartida, string? CodigoPresupuesto, int IdMoneda, string? CodigoMoneda, string? SimboloMoneda, bool EsNuevo)
{
    public static MovimientoPresupuestalResult Desde(Domain.Model.MovimientoPresupuestal m) => new(m.IdMovimientoPresupuestal,
        m.IdPresupuestoDetalle, m.IdTipoMovimientoPresupuestal, m.Tipo, m.NombreTipo, m.ClaveEvento, m.Origen, m.IdOrigen,
        m.Afectacion, m.Direccion, m.Fecha, m.Monto, m.Observacion, m.IdPresupuesto, m.IdPresupuestoVersion, m.NumeroVersion,
        m.EstadoPresupuesto, m.IdCatalogoPartida, m.CodigoPartida, m.NombrePartida, m.CodigoPresupuesto, m.IdMoneda,
        m.CodigoMoneda, m.SimboloMoneda, m.EsNuevo);
}
