namespace Cobranzas_Vittoria.Entities;
public class Compra
{
    public int IdCompra { get; set; }
    public string NumeroCompra { get; set; } = string.Empty;
    public int IdOrdenCompra { get; set; }
    public string? NumeroOrdenCompra { get; set; }
    public int IdProveedor { get; set; }
    public string? Proveedor { get; set; }
    public DateTime FechaCompra { get; set; }
    public bool Aceptada { get; set; }
    public bool IncluyeIGV { get; set; }
    public decimal SubtotalSinIGV { get; set; }
    public decimal MontoIGV { get; set; }
    public decimal MontoTotal { get; set; }
    public string? Observacion { get; set; }
    public DateTime FechaCreacion { get; set; }
}
