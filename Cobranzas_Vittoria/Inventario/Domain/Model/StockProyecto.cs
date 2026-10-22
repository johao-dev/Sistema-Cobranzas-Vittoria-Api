namespace Cobranzas_Vittoria.Inventario.Domain.Model;

public sealed class StockProyecto
{
    public int IdStockProyecto { get; set; }
    public int IdProyecto { get; set; }
    public string Proyecto { get; set; } = string.Empty;
    public int IdMaterial { get; set; }
    public string CodigoMaterial { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public int IdEspecialidad { get; set; }
    public string Especialidad { get; set; } = string.Empty;
    public decimal Stock { get; set; }
    public DateTime FechaUltimoMovimiento { get; set; }
}
