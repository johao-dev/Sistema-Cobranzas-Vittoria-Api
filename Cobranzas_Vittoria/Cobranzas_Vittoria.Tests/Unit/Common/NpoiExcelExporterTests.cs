using Cobranzas_Vittoria.Application.Common.Exports;
using Cobranzas_Vittoria.Application.Inventario.Exports;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Cobranzas_Vittoria.Tests.Unit.Common;

/// <summary>
/// Pruebas unitarias de <see cref="NpoiExcelExporter"/>.
///
/// <para>
/// Estos tests leen el archivo .xlsx generado usando NPOI (la misma
/// libreria con la que se escribio) y validan la estructura de la hoja.
/// Esto cubre la forma del archivo sin depender de instalar Office o
/// un visualizador.
/// </para>
///
/// <para>
/// <b>Layout esperado</b> (filas 0-based, default <c>HeaderRowIndex = 6</c>):
/// <code>
///   0: vacia
///   1: Title (merged, si se indico)
///   2: vacia
///   3: FiltersSubtitle (merged, si se indico)
///   4: "Generado el: ..." (merged)
///   5: vacia
///   6: HEADER de columnas
///   7..N: datos
///   N+1: fila de TOTALES (si IncludeTotalsRow = true y hay datos)
/// </code>
/// </para>
/// </summary>
public class NpoiExcelExporterTests
{
    private readonly IExcelExporter _exporter = new NpoiExcelExporter();

    // ============================================================================
    // Helpers privados
    // ============================================================================

    /// <summary>Carga un workbook desde bytes y devuelve la primera hoja.</summary>
    private static ISheet GetSheet(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var workbook = new XSSFWorkbook(ms);
        return workbook.GetSheetAt(0);
    }

    /// <summary>Devuelve el string de una celda (trata null como string vacio).</summary>
    private static string GetString(ICell? cell)
    {
        if (cell is null) return string.Empty;
        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue,
            CellType.Numeric => cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => string.Empty
        };
    }

    // ============================================================================
    // Estructura basica
    // ============================================================================

    [Test]
    public void Export_DatosVaciosYSinTotales_SoloGeneraHeader()
    {
        // Arrange
        var config = new ExcelSheetConfig
        {
            Title = "CONSOLIDADO",
            FiltersSubtitle = "Filtros: (sin filtros)",
            IncludeTotalsRow = false
        };

        // Act
        var bytes = _exporter.ExportToXlsx(Array.Empty<KardexStockExcelRow>(), config);

        // Assert
        Assert.That(bytes, Is.Not.Empty);
        var sheet = GetSheet(bytes);
        // Solo debe existir la fila de header (fila 6); el resto son null.
        Assert.That(sheet.LastRowNum, Is.EqualTo(6),
            "Sin datos y sin totales, la ultima fila escrita debe ser la del header.");
    }

    [Test]
    public void Export_TituloSubtituloGeneradoAparecenEnFilasEsperadas()
    {
        // Arrange
        var config = new ExcelSheetConfig
        {
            Title = "CONSOLIDADO DE INVENTARIO",
            FiltersSubtitle = "Filtros: idEspecialidad=2"
        };

        // Act
        var bytes = _exporter.ExportToXlsx(Array.Empty<KardexStockExcelRow>(), config);
        var sheet = GetSheet(bytes);

        // Assert
        Assert.That(GetString(sheet.GetRow(1)?.GetCell(0)), Is.EqualTo("CONSOLIDADO DE INVENTARIO"));
        Assert.That(GetString(sheet.GetRow(3)?.GetCell(0)), Is.EqualTo("Filtros: idEspecialidad=2"));
        Assert.That(GetString(sheet.GetRow(4)?.GetCell(0)), Does.StartWith("Generado el:"));
    }

    [Test]
    public void Export_SinTituloNiSubtitulo_OmiteLasFilasCorrespondientes()
    {
        // Arrange
        var config = new ExcelSheetConfig
        {
            // Title y FiltersSubtitle quedan en null
        };

        // Act
        var bytes = _exporter.ExportToXlsx(Array.Empty<KardexStockExcelRow>(), config);
        var sheet = GetSheet(bytes);
        var titleRow = sheet.GetRow(1);
        var filtersRow = sheet.GetRow(3);

        // Assert
        // Fila 1 (titulo) debe estar vacia o no existir.
        var titleText = titleRow is null ? string.Empty : GetString(titleRow.GetCell(0));
        Assert.That(titleText, Is.Empty, "Sin titulo, la fila 1 debe estar vacia.");
        // Fila 3 (filtros) debe estar vacia o no existir.
        var filtersText = filtersRow is null ? string.Empty : GetString(filtersRow.GetCell(0));
        Assert.That(filtersText, Is.Empty, "Sin subtitulo de filtros, la fila 3 debe estar vacia.");
    }

    // ============================================================================
    // Headers y orden
    // ============================================================================

    [Test]
    public void Export_HeadersEnEspanolSegunContrato()
    {
        // Arrange: usamos el DTO real del feature para validar que los
        // headers coinciden exactamente con lo que requiere el frontend.
        var config = new ExcelSheetConfig();

        // Act
        var bytes = _exporter.ExportToXlsx(Array.Empty<KardexStockExcelRow>(), config);
        var sheet = GetSheet(bytes);
        var headerRow = sheet.GetRow(6);

        // Assert: el orden debe ser el declarado por [ExcelColumn(Order = N)]
        Assert.That(GetString(headerRow.GetCell(0)), Is.EqualTo("N°"));
        Assert.That(GetString(headerRow.GetCell(1)), Is.EqualTo("Proyecto"));
        Assert.That(GetString(headerRow.GetCell(2)), Is.EqualTo("Especialidad"));
        Assert.That(GetString(headerRow.GetCell(3)), Is.EqualTo("Cód. Material"));
        Assert.That(GetString(headerRow.GetCell(4)), Is.EqualTo("Nombre"));
        Assert.That(GetString(headerRow.GetCell(5)), Is.EqualTo("Unidad Medida"));
        Assert.That(GetString(headerRow.GetCell(6)), Is.EqualTo("Entrada"));
        Assert.That(GetString(headerRow.GetCell(7)), Is.EqualTo("Salida"));
        Assert.That(GetString(headerRow.GetCell(8)), Is.EqualTo("Stock"));
        Assert.That(GetString(headerRow.GetCell(9)), Is.EqualTo("Fecha"));
    }

    [Test]
    public void Export_PropiedadesSinExcelColumnAtributo_SeExcluyen()
    {
        // Arrange: DTO de prueba con un mix de propiedades con y sin atributo.
        var rows = new[] { new DtoConPropiedadOculta { Visible = "X", Oculto = "Y" } };
        var config = new ExcelSheetConfig();

        // Act
        var bytes = _exporter.ExportToXlsx(rows, config);
        var sheet = GetSheet(bytes);
        var headerRow = sheet.GetRow(6);

        // Assert
        Assert.That(GetString(headerRow.GetCell(0)), Is.EqualTo("Visible"));
        // No debe haber segunda columna (la propiedad Oculto se ignora).
        Assert.That(headerRow.GetCell(1), Is.Null,
            "Propiedades sin [ExcelColumn] deben ignorarse.");
    }

    [Test]
    public void Export_OrdenPorAtributoOrder_Respetado()
    {
        // Arrange: DTO con Order no secuencial (5, 1, 3).
        var rows = new[] { new DtoOrdenPersonalizado { Tercero = "C", Primero = "A", Segundo = "B" } };
        var config = new ExcelSheetConfig();

        // Act
        var bytes = _exporter.ExportToXlsx(rows, config);
        var sheet = GetSheet(bytes);
        var headerRow = sheet.GetRow(6);
        var dataRow = sheet.GetRow(7);

        // Assert
        Assert.That(GetString(headerRow.GetCell(0)), Is.EqualTo("Primero"));
        Assert.That(GetString(headerRow.GetCell(1)), Is.EqualTo("Segundo"));
        Assert.That(GetString(headerRow.GetCell(2)), Is.EqualTo("Tercero"));
        Assert.That(GetString(dataRow.GetCell(0)), Is.EqualTo("A"));
        Assert.That(GetString(dataRow.GetCell(1)), Is.EqualTo("B"));
        Assert.That(GetString(dataRow.GetCell(2)), Is.EqualTo("C"));
    }

    // ============================================================================
    // Datos
    // ============================================================================

    [Test]
    public void Export_UnaFila_NumeroAsignadoYDatosCorrectos()
    {
        // Arrange
        var rows = new[]
        {
            new KardexStockExcelRow
            {
                Numero = 1,
                Proyecto = "Mayta Capac II",
                Especialidad = "Albañilería",
                CodigoMaterial = "MAT-0001",
                Nombre = "MORTERO LISTO",
                UnidadMedida = "BOL",
                Entrada = 50m,
                Salida = 3m,
                Stock = 47m,
                Fecha = new DateOnly(2026, 1, 16)
            }
        };
        var config = new ExcelSheetConfig
        {
            Title = "CONSOLIDADO DE INVENTARIO",
            FiltersSubtitle = "Filtros: idEspecialidad=2",
            IncludeTotalsRow = true
        };

        // Act
        var bytes = _exporter.ExportToXlsx(rows, config);
        var sheet = GetSheet(bytes);
        var dataRow = sheet.GetRow(7);

        // Assert
        Assert.That((int)dataRow.GetCell(0).NumericCellValue, Is.EqualTo(1));
        Assert.That(GetString(dataRow.GetCell(1)), Is.EqualTo("Mayta Capac II"));
        Assert.That(GetString(dataRow.GetCell(2)), Is.EqualTo("Albañilería"));
        Assert.That(GetString(dataRow.GetCell(3)), Is.EqualTo("MAT-0001"));
        Assert.That(GetString(dataRow.GetCell(4)), Is.EqualTo("MORTERO LISTO"));
        Assert.That(GetString(dataRow.GetCell(5)), Is.EqualTo("BOL"));
        Assert.That((decimal)dataRow.GetCell(6).NumericCellValue, Is.EqualTo(50m));
        Assert.That((decimal)dataRow.GetCell(7).NumericCellValue, Is.EqualTo(3m));
        Assert.That((decimal)dataRow.GetCell(8).NumericCellValue, Is.EqualTo(47m));
        // La fecha se serializa como DateTime (NPOI no soporta DateOnly nativamente).
        Assert.That(dataRow.GetCell(9).CellType, Is.EqualTo(CellType.Numeric));
        Assert.That(dataRow.GetCell(9).DateCellValue, Is.EqualTo(new DateTime(2026, 1, 16)));
    }

    [Test]
    public void Export_ValoresNull_CeldasVaciasSinExcepcion()
    {
        // Arrange: fila con todos los campos null salvo Numero.
        var rows = new[] { new KardexStockExcelRow { Numero = 1 } };
        var config = new ExcelSheetConfig();

        // Act
        var bytes = _exporter.ExportToXlsx(rows, config);
        var sheet = GetSheet(bytes);
        var dataRow = sheet.GetRow(7);

        // Assert
        Assert.That((int)dataRow.GetCell(0).NumericCellValue, Is.EqualTo(1));
        // Las celdas siguientes existen pero estan en blanco.
        for (var c = 1; c < 10; c++)
        {
            Assert.That(dataRow.GetCell(c), Is.Not.Null,
                $"Celda {c} debe existir aunque este en blanco.");
            Assert.That(dataRow.GetCell(c).CellType, Is.EqualTo(CellType.Blank),
                $"Celda {c} con valor null debe quedar en blanco.");
        }
    }

    // ============================================================================
    // Totales
    // ============================================================================

    [Test]
    public void Export_ConTotalesYMultiplesFilas_SumaColumnasNumericas()
    {
        // Arrange
        var rows = new[]
        {
            new KardexStockExcelRow { Numero = 1, Entrada = 10m, Salida = 2m, Stock = 8m },
            new KardexStockExcelRow { Numero = 2, Entrada = 20m, Salida = 5m, Stock = 15m },
            new KardexStockExcelRow { Numero = 3, Entrada = 30m, Salida = 7m, Stock = 23m }
        };
        var config = new ExcelSheetConfig { IncludeTotalsRow = true };

        // Act
        var bytes = _exporter.ExportToXlsx(rows, config);
        var sheet = GetSheet(bytes);
        // Header en fila 6, 3 filas de datos en 7-9, totales en fila 10.
        var totalsRow = sheet.GetRow(10);

        // Assert
        Assert.That(GetString(totalsRow.GetCell(0)), Is.EqualTo("TOTAL"),
            "La primera columna no-total debe contener la etiqueta 'TOTAL'.");
        Assert.That((decimal)totalsRow.GetCell(6).NumericCellValue, Is.EqualTo(60m),
            "Suma de Entrada: 10 + 20 + 30.");
        Assert.That((decimal)totalsRow.GetCell(7).NumericCellValue, Is.EqualTo(14m),
            "Suma de Salida: 2 + 5 + 7.");
        Assert.That((decimal)totalsRow.GetCell(8).NumericCellValue, Is.EqualTo(46m),
            "Suma de Stock: 8 + 15 + 23.");
    }

    [Test]
    public void Export_ConTotalesYValoresNullEnColumnasTotales_SumaComoCero()
    {
        // Arrange: una fila con Entrada null (se interpreta como 0 en el total).
        var rows = new[]
        {
            new KardexStockExcelRow { Numero = 1, Entrada = 10m, Salida = null, Stock = 10m },
            new KardexStockExcelRow { Numero = 2, Entrada = null, Salida = 5m, Stock = -5m }
        };
        var config = new ExcelSheetConfig { IncludeTotalsRow = true };

        // Act
        var bytes = _exporter.ExportToXlsx(rows, config);
        var sheet = GetSheet(bytes);
        var totalsRow = sheet.GetRow(9);

        // Assert
        Assert.That((decimal)totalsRow.GetCell(6).NumericCellValue, Is.EqualTo(10m));
        Assert.That((decimal)totalsRow.GetCell(7).NumericCellValue, Is.EqualTo(5m));
        Assert.That((decimal)totalsRow.GetCell(8).NumericCellValue, Is.EqualTo(5m));
    }

    [Test]
    public void Export_SinTotalesYConDatos_NoAgregaFilaDeTotales()
    {
        // Arrange
        var rows = new[]
        {
            new KardexStockExcelRow { Numero = 1, Entrada = 10m }
        };
        var config = new ExcelSheetConfig { IncludeTotalsRow = false };

        // Act
        var bytes = _exporter.ExportToXlsx(rows, config);
        var sheet = GetSheet(bytes);

        // Assert
        // Header en fila 6, dato en fila 7, NO debe haber fila 8 (totales).
        Assert.That(sheet.LastRowNum, Is.EqualTo(7),
            "Sin totales, la ultima fila debe ser la del dato (fila 7).");
    }

    [Test]
    public void Export_DatosVaciosConTotales_NoGeneraFilaDeTotales()
    {
        // Arrange: sin datos, no debe haber totales aunque IncludeTotalsRow=true.
        var config = new ExcelSheetConfig { IncludeTotalsRow = true };

        // Act
        var bytes = _exporter.ExportToXlsx(Array.Empty<KardexStockExcelRow>(), config);
        var sheet = GetSheet(bytes);

        // Assert
        Assert.That(sheet.LastRowNum, Is.EqualTo(6),
            "Sin datos, la fila de totales no debe existir aunque IncludeTotalsRow=true.");
    }

    // ============================================================================
    // Formato
    // ============================================================================

    [Test]
    public void Export_ColumnaNumerica_TieneFormatoConDosDecimales()
    {
        // Arrange
        var rows = new[] { new KardexStockExcelRow { Numero = 1, Entrada = 1234.5678m } };
        var config = new ExcelSheetConfig();

        // Act
        var bytes = _exporter.ExportToXlsx(rows, config);
        var sheet = GetSheet(bytes);
        var dataRow = sheet.GetRow(7);
        var cell = dataRow.GetCell(6);

        // Assert
        var dataFormat = cell.CellStyle.GetDataFormatString();
        Assert.That(dataFormat, Is.EqualTo("#,##0.00"));
    }

    [Test]
    public void Export_ColumnaFecha_TieneFormatoDdMmAaaa()
    {
        // Arrange
        var rows = new[] { new KardexStockExcelRow { Numero = 1, Fecha = new DateOnly(2026, 1, 16) } };
        var config = new ExcelSheetConfig();

        // Act
        var bytes = _exporter.ExportToXlsx(rows, config);
        var sheet = GetSheet(bytes);
        var dataRow = sheet.GetRow(7);
        var cell = dataRow.GetCell(9);

        // Assert
        var dataFormat = cell.CellStyle.GetDataFormatString();
        Assert.That(dataFormat, Is.EqualTo("dd/MM/yyyy"));
    }

    [Test]
    public void Export_BytesComienzanConFirmaPK_SonUnArchivoXlsxValido()
    {
        // Arrange: un archivo .xlsx es un zip, asi que sus primeros bytes son "PK".
        var bytes = _exporter.ExportToXlsx(Array.Empty<KardexStockExcelRow>(), new ExcelSheetConfig());

        // Assert
        Assert.That(bytes.Length, Is.GreaterThan(4));
        Assert.That(bytes[0], Is.EqualTo((byte)'P'));
        Assert.That(bytes[1], Is.EqualTo((byte)'K'));
    }

    // ============================================================================
    // DTOs de prueba
    // ============================================================================

    private sealed class DtoConPropiedadOculta
    {
        [ExcelColumn(Header = "Visible", Order = 0)]
        public string? Visible { get; set; }

        // Sin [ExcelColumn]: debe ignorarse.
        public string? Oculto { get; set; }
    }

    private sealed class DtoOrdenPersonalizado
    {
        [ExcelColumn(Header = "Tercero", Order = 5)]
        public string? Tercero { get; set; }

        [ExcelColumn(Header = "Primero", Order = 1)]
        public string? Primero { get; set; }

        [ExcelColumn(Header = "Segundo", Order = 3)]
        public string? Segundo { get; set; }
    }
}
