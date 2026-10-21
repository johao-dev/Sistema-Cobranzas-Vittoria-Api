namespace Cobranzas_Vittoria.Inventario.Domain.Model;

/// <summary>Stock consolidado por material, con la especialidad derivada de Material, con los totales de entradas y salidas.</summary>
public sealed class StockKardex
{
    public int IdKardexStock { get; set; }
    public int IdMaterial { get; set; }
    public string? CodigoMaterial { get; set; }
    public string? Nombre { get; set; }
    public string? UnidadMedida { get; set; }
    public int IdEspecialidad { get; set; }
    public string? Especialidad { get; set; }
    public decimal TotalEntrada { get; set; }
    public decimal TotalSalida { get; set; }
    public decimal Stock { get; set; }
    public DateOnly FechaUltimaMovimiento { get; set; }
}
