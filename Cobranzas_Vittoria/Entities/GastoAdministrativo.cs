namespace Cobranzas_Vittoria.Entities;

public class GastoAdministrativo
{
    public int IdGastoAdministrativo { get; set; }
    public int IdCategoriaGasto { get; set; }
    public string? Categoria { get; set; }
    public int IdProveedorGastoAdministrativo { get; set; }
    public string? Proveedor { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Monto { get; set; }
    public string? Descripcion { get; set; }
    public string Moneda { get; set; } = "PEN";
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int TotalFacturas { get; set; }
    public int TotalPagos { get; set; }
}
