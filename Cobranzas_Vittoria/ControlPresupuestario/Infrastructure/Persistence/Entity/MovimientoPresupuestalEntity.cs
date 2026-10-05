namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Fila del ledger (usp_MovimientoPresupuestal_*).</summary>
public sealed class MovimientoPresupuestalEntity
{
    public long IdMovimientoPresupuestal { get; set; }
    public int IdPresupuestoDetalle { get; set; }
    public int IdTipoMovimientoPresupuestal { get; set; }
    public string? TipoMovimiento { get; set; }
    public string? NombreTipoMovimiento { get; set; }
    public string? ClaveEvento { get; set; }
    public string? Origen { get; set; }
    public int IdOrigen { get; set; }
    public string? Afectacion { get; set; }
    public string? Direccion { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Monto { get; set; }
    public string? Observacion { get; set; }
    public int IdPresupuesto { get; set; }
    public int IdPresupuestoVersion { get; set; }
    public int NumeroVersion { get; set; }
    public string? EstadoPresupuesto { get; set; }
    public int IdCatalogoPartida { get; set; }
    public string? CodigoPartida { get; set; }
    public string? NombrePartida { get; set; }
    public string? CodigoPresupuesto { get; set; }
    public int IdMoneda { get; set; }
    public string? CodigoMoneda { get; set; }
    public string? SimboloMoneda { get; set; }
    public bool EsNuevo { get; set; }
}
