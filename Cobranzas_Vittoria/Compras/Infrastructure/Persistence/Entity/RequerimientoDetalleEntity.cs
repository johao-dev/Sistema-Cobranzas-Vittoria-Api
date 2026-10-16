namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Entity;

public class RequerimientoDetalleEntity
{
    public int IdRequerimientoDetalle { get; set; }
    public int IdRequerimiento { get; set; }
    public int IdMaterial { get; set; }
    public decimal Cantidad { get; set; }
    // Dapper puede omitir columnas NULL; el valor por defecto también debe ser null.
    public string? Observacion { get; set; }
    public int? IdPresupuestoDetalle { get; set; }
}
