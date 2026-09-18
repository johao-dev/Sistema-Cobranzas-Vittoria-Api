namespace Cobranzas_Vittoria.Dtos.Compras;

public class OrdenCompraUpdateDto
{
    public string NumeroOrdenCompra { get; set; } = string.Empty;
    public int IdRequerimiento { get; set; }
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int IdMoneda { get; set; }
    public DateTime FechaOrdenCompra { get; set; }
    public string? Descripcion { get; set; }
    public int? IdUsuarioCreacion { get; set; }
    public string? RutaPdf { get; set; }
    public List<OrdenCompraDetalleUpdateDto> Items { get; set; } = new();
}

public class OrdenCompraDetalleUpdateDto
{
    public int IdMaterial { get; set; }
    public decimal Cantidad { get; set; }
    public int IdProveedor { get; set; }
    public decimal PrecioUnitario { get; set; }
}
