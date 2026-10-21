namespace Cobranzas_Vittoria.Inventario.Presentation.Dto;

/// <summary>Respuesta HTTP del stock global por material y especialidad, con entradas y salidas acumuladas.</summary>
public sealed class KardexStockActualResponseDto
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
