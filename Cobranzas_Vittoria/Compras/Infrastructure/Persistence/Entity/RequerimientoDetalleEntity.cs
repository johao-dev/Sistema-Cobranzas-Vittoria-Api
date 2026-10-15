namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Entity;

public class RequerimientoDetalleEntity
{
    public int IdRequerimientoDetalle { get; set; }
    public int IdRequerimiento { get; set; }
    public int IdMaterial { get; set; }
    public decimal Cantidad { get; set; }
    public string? Observacion { get; set; } = string.Empty;
    public int? IdPresupuestoDetalle { get; set; }
}