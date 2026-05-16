namespace Cobranzas_Vittoria.Entities;
public class OrdenCompraDetalle
{
    public int IdOrdenCompraDetalle { get; set; }
    public int IdOrdenCompra { get; set; }
    public int IdMaterial { get; set; }
    public int IdProveedor { get; set; }
    public string? Proveedor { get; set; }
    public string? Material { get; set; }
    public string? Especialidad { get; set; }
    public string? UnidadMedida { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}
