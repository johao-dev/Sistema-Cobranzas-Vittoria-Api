namespace Cobranzas_Vittoria.Dtos.Compras;

public class CompraCreateDto
{
    public string NumeroCompra { get; set; } = string.Empty;
    public int IdOrdenCompra { get; set; }
    public int IdProveedor { get; set; }
    public DateTime FechaCompra { get; set; }
    public bool IncluyeIGV { get; set; }
    public decimal SubtotalSinIGV { get; set; }
    public decimal MontoIGV { get; set; }
    public decimal MontoTotal { get; set; }
    public string? Observacion { get; set; }
    public List<CompraDetalleCreateDto> Items { get; set; } = new();
}

public class CompraDetalleCreateDto
{
    public int IdMaterial { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
}
