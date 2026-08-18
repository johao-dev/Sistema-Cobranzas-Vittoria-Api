using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Application.Common.Exports;
using Cobranzas_Vittoria.Application.Inventario.Dtos;
using Cobranzas_Vittoria.Tests.Integration.Common;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Cobranzas_Vittoria.Tests.Integration.Almacen;

/// <summary>
/// Pruebas de integracion de <c>GET /api/almacen/kardex/stock-actual/exportar-excel</c>.
///
/// <para>
/// El endpoint produce un archivo XLSX (generado por NPOI) con el mismo
/// consolidado que devuelve <c>GET /stock-actual</c>. Estos tests:
/// </para>
/// <list type="bullet">
///   <item>Verifican codigos HTTP y content-type.</item>
///   <item>Validan que el body es un XLSX valido (firma PK + parseable por NPOI).</item>
///   <item>Validan la estructura de la hoja (titulo, subtitulo de filtros, header, datos, totales).</item>
///   <item>Validan que el subtitulo refleja los filtros aplicados (idEspecialidad, idProyecto, fecha).</item>
///   <item>Validan que el numero de filas de datos coincide con GET /stock-actual.</item>
/// </list>
///
/// <para>
/// <b>Patron de setup</b>: se siguen las mismas convenciones que
/// <c>KardexInventarioControllerTests</c> (entradas via API, IdMaterial=2 de
/// la seed para Albañileria). El KardexStock es por triada
/// (IdMaterial, IdEspecialidad, IdProyecto), por lo que todas las entradas
/// de prueba usan <c>IdProyecto=10</c> (Mayta Capac II).
/// </para>
/// </summary>
public class KardexInventarioExportTests : IntegrationTestBase
{
    private const int IdMaterialAlbanileria = 2; // "MORTERO LISTO" (Albañileria, seed)
    private const int IdMaterialAlbanileria2 = 3; // "PLASTICO AZUL" (Albañileria, seed)
    private const int IdEspecialidadAlbanileria = SeedIds.EspecialidadAlbanileria;
    private const int IdProyecto = SeedIds.ProyectoMaytaCapacII;
    private const int IdProveedor = 2; // "ACG EDIFICACIONES EIRL" (Activo=1)

    // ============================================================================
    // Helpers
    // ============================================================================

    private static KardexEntradaCreateDto EntradaValida(
        decimal cantidad,
        int idMaterial = IdMaterialAlbanileria,
        int idEspecialidad = IdEspecialidadAlbanileria)
        => new()
        {
            IdKardexEntrada = null,
            IdEspecialidad = idEspecialidad,
            IdMaterial = idMaterial,
            IdProveedor = IdProveedor,
            IdProyecto = IdProyecto,
            NumeroDocumento = "F001-EXPORT",
            Fecha = new DateOnly(2026, 1, 15),
            Cantidad = cantidad,
            Observacion = "Entrada para test de export"
        };

    private async Task CrearEntradaAsync(decimal cantidad, int idMaterial = IdMaterialAlbanileria, int idEspecialidad = IdEspecialidadAlbanileria)
    {
        var dto = EntradaValida(cantidad, idMaterial, idEspecialidad);
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/entradas", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup fallo al crear entrada. Body: {await response.Content.ReadAsStringAsync()}");
    }

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

    private static ISheet GetSheet(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var workbook = new XSSFWorkbook(ms);
        return workbook.GetSheetAt(0);
    }

    // ============================================================================
    // Smoke tests del endpoint
    // ============================================================================

    [Test]
    public async Task ExportarStockActual_SinDatos_Retorna200ConXlsxValidoConHeader()
    {
        // Act
        var response = await _client.GetAsync("/api/almacen/kardex/stock-actual/exportar-excel");

        // Assert: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

        // Assert: archivo XLSX valido
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.That(bytes, Is.Not.Empty);
        Assert.That(bytes[0], Is.EqualTo((byte)'P'), "Firma PK del zip XLSX.");
        Assert.That(bytes[1], Is.EqualTo((byte)'K'));

        // Assert: estructura minima
        var sheet = GetSheet(bytes);
        Assert.That(sheet.SheetName, Is.EqualTo("Kardex Stock"));
        // Header en fila 6 (default de ExcelSheetConfig.HeaderRowIndex).
        var headerRow = sheet.GetRow(6);
        Assert.That(headerRow, Is.Not.Null);
        Assert.That(GetString(headerRow.GetCell(0)), Is.EqualTo("N°"));
    }

    [Test]
    public async Task ExportarStockActual_ConDatos_GeneraFilasConNumerosYColumnasEsperadas()
    {
        // Arrange: 2 entradas de materiales distintos, ambos de Albañileria.
        await CrearEntradaAsync(cantidad: 10m, idMaterial: IdMaterialAlbanileria);
        await CrearEntradaAsync(cantidad: 5m, idMaterial: IdMaterialAlbanileria2);

        // Act
        var response = await _client.GetAsync("/api/almacen/kardex/stock-actual/exportar-excel");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var sheet = GetSheet(bytes);

        // Verificamos headers en el orden correcto.
        var headerRow = sheet.GetRow(6);
        var headersEsperados = new[] { "N°", "Proyecto", "Especialidad", "Cód. Material", "Nombre", "Unidad Medida", "Entrada", "Salida", "Stock", "Fecha" };
        for (var c = 0; c < headersEsperados.Length; c++)
        {
            Assert.That(GetString(headerRow.GetCell(c)), Is.EqualTo(headersEsperados[c]),
                $"Header en columna {c} no coincide.");
        }

        // 2 filas de datos (filas 7 y 8). Como KardexStock es por triada
        // (IdMaterial, IdEspecialidad, IdProyecto), cada material aparece
        // en una fila propia.
        var dataRow1 = sheet.GetRow(7);
        var dataRow2 = sheet.GetRow(8);
        Assert.That(dataRow1, Is.Not.Null);
        Assert.That(dataRow2, Is.Not.Null);
        Assert.That((int)dataRow1.GetCell(0).NumericCellValue, Is.EqualTo(1));
        Assert.That((int)dataRow2.GetCell(0).NumericCellValue, Is.EqualTo(2));

        // Columna "Entrada" (indice 6) refleja la cantidad de la entrada.
        Assert.That((decimal)dataRow1.GetCell(6).NumericCellValue, Is.EqualTo(10m));
        Assert.That((decimal)dataRow2.GetCell(6).NumericCellValue, Is.EqualTo(5m));

        // Fila de totales (fila 9): "TOTAL" en col 0, suma de Entradas = 15.
        var totalsRow = sheet.GetRow(9);
        Assert.That(totalsRow, Is.Not.Null);
        Assert.That(GetString(totalsRow.GetCell(0)), Is.EqualTo("TOTAL"));
        Assert.That((decimal)totalsRow.GetCell(6).NumericCellValue, Is.EqualTo(15m));
    }

    [Test]
    public async Task ExportarStockActual_FiltroPorIdEspecialidad_SoloIncluyeFilasDeEsaEspecialidad()
    {
        // Arrange: una entrada de Albañileria. Filtraremos por Casco (sin datos).
        await CrearEntradaAsync(cantidad: 10m, idMaterial: IdMaterialAlbanileria);

        // Act: filtramos por Casco (idEspecialidad=4), no debe haber resultados
        var response = await _client.GetAsync(
            $"/api/almacen/kardex/stock-actual/exportar-excel?idEspecialidad={SeedIds.EspecialidadCasco}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var sheet = GetSheet(bytes);

        // Sin datos, no debe haber fila de totales. La ultima fila escrita
        // debe ser la del header (fila 6).
        Assert.That(sheet.LastRowNum, Is.EqualTo(6),
            "Sin filas para el filtro, no debe haber fila de totales.");

        // El subtitulo de filtros debe mencionar el idEspecialidad aplicado.
        var filtersRow = sheet.GetRow(3);
        Assert.That(GetString(filtersRow.GetCell(0)),
            Does.Contain($"idEspecialidad={SeedIds.EspecialidadCasco}"));
    }

    [Test]
    public async Task ExportarStockActual_FiltroPorIdProyecto_SubtituloIncluyeIdProyecto()
    {
        // Arrange
        await CrearEntradaAsync(cantidad: 10m);

        // Act: filtramos por IdProyecto
        var response = await _client.GetAsync(
            $"/api/almacen/kardex/stock-actual/exportar-excel?idProyecto={IdProyecto}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var sheet = GetSheet(bytes);

        var filtersRow = sheet.GetRow(3);
        Assert.That(GetString(filtersRow.GetCell(0)), Does.Contain($"idProyecto={IdProyecto}"));
    }

    [Test]
    public async Task ExportarStockActual_FiltroPorFechas_SubtituloIncluyeRango()
    {
        // Arrange
        await CrearEntradaAsync(cantidad: 10m);

        // Act
        var response = await _client.GetAsync(
            "/api/almacen/kardex/stock-actual/exportar-excel?fechaDesde=2026-01-01&fechaHasta=2026-12-31");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var sheet = GetSheet(bytes);

        var filtersRow = sheet.GetRow(3);
        Assert.That(GetString(filtersRow.GetCell(0)),
            Does.Contain("fecha=2026-01-01..2026-12-31"));
    }

    [Test]
    public async Task ExportarStockActual_SinFiltros_SubtituloDiceSinFiltros()
    {
        // Arrange
        await CrearEntradaAsync(cantidad: 10m);

        // Act
        var response = await _client.GetAsync("/api/almacen/kardex/stock-actual/exportar-excel");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var sheet = GetSheet(bytes);

        var filtersRow = sheet.GetRow(3);
        Assert.That(GetString(filtersRow.GetCell(0)), Is.EqualTo("Filtros: (sin filtros)"));
    }

    [Test]
    public async Task ExportarStockActual_TituloEsConsolidadoDeInventario()
    {
        // Arrange
        await CrearEntradaAsync(cantidad: 10m);

        // Act
        var response = await _client.GetAsync("/api/almacen/kardex/stock-actual/exportar-excel");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var sheet = GetSheet(bytes);

        // Fila 1 contiene el titulo (merged).
        var titleRow = sheet.GetRow(1);
        Assert.That(GetString(titleRow.GetCell(0)), Is.EqualTo("CONSOLIDADO DE INVENTARIO"));
    }

    [Test]
    public async Task ExportarStockActual_FechaGeneradoApareceEnSubtitulo()
    {
        // Arrange
        await CrearEntradaAsync(cantidad: 10m);

        // Act
        var response = await _client.GetAsync("/api/almacen/kardex/stock-actual/exportar-excel");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var sheet = GetSheet(bytes);

        // Fila 4 contiene "Generado el: ..." (subtitulo automatico).
        var generatedAtRow = sheet.GetRow(4);
        Assert.That(GetString(generatedAtRow.GetCell(0)), Does.StartWith("Generado el:"));
    }

    [Test]
    public async Task ExportarStockActual_ContentDispositionIncluyeNombreArchivoConExtensionXlsx()
    {
        // Act
        var response = await _client.GetAsync("/api/almacen/kardex/stock-actual/exportar-excel");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var contentDisposition = response.Content.Headers.ContentDisposition;
        Assert.That(contentDisposition, Is.Not.Null);
        Assert.That(contentDisposition!.FileName, Does.EndWith(".xlsx"));
        Assert.That(contentDisposition.FileName, Does.Contain("kardex-stock-"));
    }

    [Test]
    public async Task ExportarStockActual_NumeroDeFilasDeDatos_CoincideConGetStockActual()
    {
        // Arrange: 3 entradas de materiales diferentes (mismo proyecto y especialidad).
        await CrearEntradaAsync(cantidad: 10m, idMaterial: IdMaterialAlbanileria);
        await CrearEntradaAsync(cantidad: 5m, idMaterial: IdMaterialAlbanileria2);
        // Material 4 deberia ser de Albañileria segun seed (verificar en KardexInventarioControllerTests).

        // Act 1: contar filas via GET /stock-actual
        var listResponse = await _client.GetAsync(
            $"/api/almacen/kardex/stock-actual?idEspecialidad={IdEspecialidadAlbanileria}&idProyecto={IdProyecto}");
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var filasEnJson = listBody.GetArrayLength();

        // Act 2: descargar XLSX y contar filas de datos
        var exportResponse = await _client.GetAsync(
            $"/api/almacen/kardex/stock-actual/exportar-excel?idEspecialidad={IdEspecialidadAlbanileria}&idProyecto={IdProyecto}");
        var bytes = await exportResponse.Content.ReadAsByteArrayAsync();
        var sheet = GetSheet(bytes);

        // Header en fila 6; datos en filas 7..(7+N-1); totales en fila 7+N.
        var filasEnExcel = sheet.LastRowNum - 6 - 1; // -1 por totales
        if (filasEnJson == 0) filasEnExcel = 0;

        // Assert
        Assert.That(filasEnExcel, Is.EqualTo(filasEnJson),
            "El numero de filas de datos en el Excel debe coincidir con GET /stock-actual.");
    }
}
