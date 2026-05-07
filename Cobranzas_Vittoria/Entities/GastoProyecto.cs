namespace Cobranzas_Vittoria.Entities;

public class GastoProyecto
{
    public int IdGastoProyecto { get; set; }
    public string TipoModulo { get; set; } = string.Empty;
    public int IdProyecto { get; set; }
    public string? Proyecto { get; set; }
    public int? IdProveedorTerreno { get; set; }
    public string? Proveedor { get; set; }
    public DateTime Fecha { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string Moneda { get; set; } = "PEN";
    public decimal MontoSoles { get; set; }
    public decimal MontoDolares { get; set; }
    public DateTime? FechaTipoCambio { get; set; }
    public decimal TipoCambio { get; set; }
    public string? Descripcion { get; set; }
    public string Estado { get; set; } = "Activo";
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int TotalFacturas { get; set; }
}
