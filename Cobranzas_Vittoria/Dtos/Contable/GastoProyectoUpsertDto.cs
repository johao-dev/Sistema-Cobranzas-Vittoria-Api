namespace Cobranzas_Vittoria.Dtos.Contable;

public class GastoProyectoUpsertDto
{
    public int? IdGastoProyecto { get; set; }
    public string? TipoModulo { get; set; }
    public int IdProyecto { get; set; }
    public int? IdProveedorTerreno { get; set; }
    public DateTime Fecha { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string Moneda { get; set; } = "PEN";
    public decimal MontoSoles { get; set; }
    public decimal MontoDolares { get; set; }
    public DateTime? FechaTipoCambio { get; set; }
    public decimal TipoCambio { get; set; } = 3.41m;
    public string? Descripcion { get; set; }
    public string Estado { get; set; } = "Activo";
    public bool Activo { get; set; } = true;
}
