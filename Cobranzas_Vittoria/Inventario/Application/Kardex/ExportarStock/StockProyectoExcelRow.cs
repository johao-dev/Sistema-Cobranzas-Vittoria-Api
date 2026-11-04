using Cobranzas_Vittoria.Application.Common.Exports;

namespace Cobranzas_Vittoria.Inventario.Application.Kardex.ExportarStock;

/// <summary>Columnas de la distribución actual; no contiene entradas ni salidas globales.</summary>
public sealed class StockProyectoExcelRow
{
    [ExcelColumn(Header = "N°", Order = 0)]
    public int Numero { get; set; }

    [ExcelColumn(Header = "Especialidad", Order = 1)]
    public string Especialidad { get; set; } = string.Empty;

    [ExcelColumn(Header = "Cód. Material", Order = 2)]
    public string CodigoMaterial { get; set; } = string.Empty;

    [ExcelColumn(Header = "Nombre", Order = 3)]
    public string Nombre { get; set; } = string.Empty;

    [ExcelColumn(Header = "Unidad Medida", Order = 4)]
    public string UnidadMedida { get; set; } = string.Empty;

    [ExcelColumn(Header = "Proyecto", Order = 5)]
    public string Proyecto { get; set; } = string.Empty;

    [ExcelColumn(Header = "Stock", Order = 6, Format = "#,##0.00")]
    public decimal Stock { get; set; }

    [ExcelColumn(Header = "Fecha Últ. Movimiento", Order = 7, Format = "dd/MM/yyyy")]
    public DateTime FechaUltimoMovimiento { get; set; }
}
