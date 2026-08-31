using Cobranzas_Vittoria.Application.Common.Exports;

namespace Cobranzas_Vittoria.Application.Inventario.Exports;

/// <summary>
/// DTO de exportacion del Kardex Stock Actual a Excel.
/// Es un DTO dedicado (no reusa <c>KardexStockActualResponseDto</c>) porque
/// los encabezados, el orden y los formatos de columna son parte del
/// contrato del Excel, no del modelo de respuesta JSON.
///
/// <para>
/// <b>Mapeo a columnas del Excel</b> (orden, encabezado, formato):
/// <code>
///   1. N°              Numero              (int,  0, sin formato)
///   2. Especialidad    Especialidad        (text, sin formato)
///   3. Cód. Material   CodigoMaterial      (text, sin formato)
///   4. Nombre          Nombre              (text, sin formato)
///   5. Unidad Medida   UnidadMedida        (text, sin formato)
///   6. Entrada         Entrada             (decimal, totals, #,##0.00)
///   7. Salida          Salida              (decimal, totals, #,##0.00)
///   8. Stock           Stock               (decimal, totals, #,##0.00)
///   9. Fecha           Fecha               (DateOnly, dd/MM/yyyy)
/// </code>
/// </para>
///
/// <para>
/// <b>Por que <c>Numero</c> es <c>int?</c> y no <c>int</c></b>:
/// el helper generico de exportacion trata <c>null</c> como celda vacia.
/// Mantener el campo nullable permite que el service lo asigne antes de
/// delegar al exporter sin que el compilador se queje por asignar fuera
/// del constructor.
/// </para>
///
/// <para>
/// <b>Por que el resto son <c>string?</c> y <c>decimal?</c></b>:
/// los LEFT JOIN del SP <c>usp_Kardex_StockActual_Listar</c> pueden
/// devolver <c>NULL</c> en <c>proyecto</c>, <c>codigoMaterial</c>, etc.
/// Reflejar la nulabilidad del SP en el DTO evita excepciones y permite
/// que el exporter escriba celdas vacias en lugar de "null".
/// </para>
/// </summary>
public sealed class KardexStockExcelRow
{
    /// <summary>
    /// Numero de fila (1-based). Lo asigna el service con
    /// <c>Select((s, i) =&gt; new KardexStockExcelRow { Numero = i + 1, ... })</c>.
    /// </summary>
    [ExcelColumn(Header = "N°", Order = 0)]
    public int? Numero { get; set; }

    [ExcelColumn(Header = "Especialidad", Order = 1)]
    public string? Especialidad { get; set; }

    [ExcelColumn(Header = "Cód. Material", Order = 2)]
    public string? CodigoMaterial { get; set; }

    [ExcelColumn(Header = "Nombre", Order = 3)]
    public string? Nombre { get; set; }

    [ExcelColumn(Header = "Unidad Medida", Order = 4)]
    public string? UnidadMedida { get; set; }

    [ExcelColumn(Header = "Entrada", Order = 5, Format = "#,##0.00", IncludeInTotals = true)]
    public decimal? Entrada { get; set; }

    [ExcelColumn(Header = "Salida", Order = 6, Format = "#,##0.00", IncludeInTotals = true)]
    public decimal? Salida { get; set; }

    [ExcelColumn(Header = "Stock", Order = 7, Format = "#,##0.00", IncludeInTotals = true)]
    public decimal? Stock { get; set; }

    [ExcelColumn(Header = "Fecha", Order = 8, Format = "dd/MM/yyyy")]
    public DateOnly? Fecha { get; set; }
}
