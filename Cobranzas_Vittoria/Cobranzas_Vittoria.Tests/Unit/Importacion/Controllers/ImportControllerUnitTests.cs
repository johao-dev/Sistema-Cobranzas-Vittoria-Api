using Cobranzas_Vittoria.Application.Common.Exports;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Cobranzas_Vittoria.Controllers;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NPOI.XSSF.UserModel;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Controllers;

/// <summary>
/// Pruebas unitarias de <see cref="ImportController"/>.
///
/// El controller es un wrapper delgado sobre <see cref="IImportService"/>:
/// la logica de negocio esta en el service, y el manejo de errores en el
/// <c>ApiExceptionMiddleware</c>. Por eso los tests verifican:
///
///   1. El controller invoca al service con los parametros correctos
///      (modulo, archivo, usuario).
///   2. Devuelve 200 OK con el <see cref="ResultadoImportacion"/>.
///   3. Las excepciones tipadas del service se propagan SIN ser envueltas
///      (no hay try/catch en el action): el middleware las traduce a HTTP.
///
/// Los tests son a nivel de action method, no de HTTP pipeline. La traduccion
/// de excepcion -> HTTP code se cubre con <c>ApiExceptionMiddlewareTests</c> y
/// los tests de integracion end-to-end con Testcontainers (Fase 6).
/// </summary>
public class ImportControllerUnitTests
{
    private readonly RecordingImportService _service;
    private readonly ImportController _controller;

    public ImportControllerUnitTests()
    {
        _service = new RecordingImportService();
        _controller = new ImportController(
            _service,
            new NpoiExcelExporter(),
            NullLogger<ImportController>.Instance);
    }

    // =========================================================================
    // Happy path
    // =========================================================================

    [Test]
    public async Task Importar_ModuloValido_Retorna200OkConResultado()
    {
        // Arrange
        var archivo = TestFormFiles.FromText("Codigo,Nombre\nUM-001,Kg\n", "test.csv");
        _service.ResultadoARetornar = new ResultadoImportacion("unidad-medida", "csv", FilasInsertadas: 3);

        // Act
        var actionResult = await _controller.Importar("unidad-medida", archivo, "u1", CancellationToken.None);

        // Assert: tipo y status code
        var ok = actionResult as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.StatusCode, Is.EqualTo(200));

        // Assert: body
        var resultado = (ResultadoImportacion)ok.Value!;
        Assert.That(resultado.Modulo, Is.EqualTo("unidad-medida"));
        Assert.That(resultado.Formato, Is.EqualTo("csv"));
        Assert.That(resultado.FilasInsertadas, Is.EqualTo(3));

        // Assert: delega al service con los parametros correctos
        Assert.That(_service.Llamadas, Is.EqualTo(1));
        Assert.That(_service.UltimoModulo, Is.EqualTo("unidad-medida"));
        Assert.That(_service.UltimoArchivo, Is.SameAs(archivo));
        Assert.That(_service.UltimoUsuario, Is.EqualTo("u1"));
    }

    [Test]
    public async Task Importar_PropagaCancellationTokenAlService()
    {
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");
        _service.ResultadoARetornar = new ResultadoImportacion("unidad-medida", "csv", 0);

        using var cts = new CancellationTokenSource();
        await _controller.Importar("unidad-medida", archivo, "u1", cts.Token);

        Assert.That(_service.UltimoCancellationToken, Is.EqualTo(cts.Token));
    }

    // =========================================================================
    // Propagacion de excepciones del service
    // =========================================================================

    [Test]
    public void Importar_ServiceLanzaArchivoInvalidoException_PropagaExcepcion()
    {
        // El ApiExceptionMiddleware traduce esto a 400 (o 413 si codigo = TAMANIO_EXCEDIDO).
        var archivo = TestFormFiles.FromText("contenido", "datos.txt", "text/plain");
        _service.ExcepcionALanzar = new ArchivoInvalidoException("EXTENSION_INVALIDA", "Extension no permitida.");

        Assert.ThrowsAsync<ArchivoInvalidoException>(async () =>
            await _controller.Importar("unidad-medida", archivo, "u1", CancellationToken.None));
    }

    [Test]
    public void Importar_ServiceLanzaEstructuraInvalidaException_PropagaExcepcion()
    {
        // El ApiExceptionMiddleware traduce esto a 400.
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");
        _service.ExcepcionALanzar = new EstructuraInvalidaException("ENCABEZADOS_INCORRECTOS", "Falta la columna Nombre.");

        Assert.ThrowsAsync<EstructuraInvalidaException>(async () =>
            await _controller.Importar("unidad-medida", archivo, "u1", CancellationToken.None));
    }

    [Test]
    public void Importar_ServiceLanzaDatosInvalidosException_PropagaExcepcion()
    {
        // El ApiExceptionMiddleware traduce esto a 422 con la lista de errores por fila.
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");
        var errores = new[]
        {
            new DetalleErrorFila(2, "Codigo", "CAMPO_REQUERIDO", "Codigo es requerido."),
            new DetalleErrorFila(3, "Nombre", "FORMATO_INVALIDO", "Formato invalido.")
        };
        _service.ExcepcionALanzar = new DatosInvalidosException("2 filas con errores", errores);

        Assert.ThrowsAsync<DatosInvalidosException>(async () =>
            await _controller.Importar("unidad-medida", archivo, "u1", CancellationToken.None));
    }

    [Test]
    public void Importar_ServiceLanzaModuloNoSoportadoException_PropagaExcepcion()
    {
        // El ApiExceptionMiddleware traduce esto a 400 con codigo MODULO_NO_SOPORTADO.
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");
        _service.ExcepcionALanzar = new ModuloNoSoportadoException("El modulo 'foo' no es soportado.");

        Assert.ThrowsAsync<ModuloNoSoportadoException>(async () =>
            await _controller.Importar("foo", archivo, "u1", CancellationToken.None));
    }

    [Test]
    public void Importar_ServiceLanzaExcepcionGenerica_PropagaExcepcion()
    {
        // El ApiExceptionMiddleware traduce esto a 500 UNHANDLED_ERROR.
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");
        _service.ExcepcionALanzar = new InvalidOperationException("Fallo inesperado.");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _controller.Importar("unidad-medida", archivo, "u1", CancellationToken.None));
    }

    // =========================================================================
    // Validaciones de constructor
    // =========================================================================

    [Test]
    public void Constructor_ServiceNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportController(
            service: null!,
            excelExporter: new NpoiExcelExporter(),
            logger: NullLogger<ImportController>.Instance));
    }

    [Test]
    public void Constructor_ExcelExporterNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportController(
            service: new RecordingImportService(),
            excelExporter: null!,
            logger: NullLogger<ImportController>.Instance));
    }

    // =========================================================================
    // Endpoint DescargarPlantilla (Fase 4)
    // =========================================================================

    [Test]
    public async Task DescargarPlantilla_FormatoCsv_RetornaFileConBomyHeaders()
    {
        var result = await _controller.DescargarPlantilla("material", "csv", CancellationToken.None);

        var file = result as FileContentResult;
        Assert.That(file, Is.Not.Null, "Se esperaba FileContentResult.");
        Assert.That(file!.ContentType, Is.EqualTo("text/csv; charset=utf-8"));
        Assert.That(file.FileDownloadName, Does.StartWith("plantilla-materiales-"));
        Assert.That(file.FileDownloadName, Does.EndWith(".csv"));

        // Verificamos BOM UTF-8 (0xEF 0xBB 0xBF)
        Assert.That(file.FileContents.Length, Is.GreaterThanOrEqualTo(3));
        Assert.That(file.FileContents[0], Is.EqualTo((byte)0xEF));
        Assert.That(file.FileContents[1], Is.EqualTo((byte)0xBB));
        Assert.That(file.FileContents[2], Is.EqualTo((byte)0xBF));

        // Cuerpo decodificado: una sola linea con los 4 headers separados por ';'.
        // El BOM UTF-8 (3 bytes) se decodifica como U+FEFF (zero-width no-break
        // space). Lo removemos antes de comparar.
        var texto = System.Text.Encoding.UTF8.GetString(file.FileContents);
        if (texto.Length > 0 && texto[0] == '\uFEFF') texto = texto[1..];
        var primeraLineaReal = texto.Replace("\r", string.Empty).Split('\n')[0];
        Assert.That(primeraLineaReal, Is.EqualTo("Especialidad;Nombre;UnidadMedida;Codigo"));
    }

    [Test]
    public async Task DescargarPlantilla_FormatoXlsx_RetornaFileConHeadersNpoi()
    {
        var result = await _controller.DescargarPlantilla("material", "xlsx", CancellationToken.None);

        var file = result as FileContentResult;
        Assert.That(file, Is.Not.Null, "Se esperaba FileContentResult.");
        Assert.That(file!.ContentType, Is.EqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        Assert.That(file.FileDownloadName, Does.EndWith(".xlsx"));

        // Leemos el archivo con NPOI y validamos la fila de headers.
        using var ms = new MemoryStream(file.FileContents);
        var workbook = new XSSFWorkbook(ms);
        var sheet = workbook.GetSheetAt(0);

        // Layout del helper NpoiExcelExporter (constante del helper, no se
        // puede cambiar sin modificarlo): con Title + FiltersSubtitle +
        // GeneratedAt activos, el header queda en la fila 5 (filas 0,1,2,3,4
        // ocupadas por margen/titulo/vacia/subtitulo/fecha).
        var headerRow = sheet.GetRow(5);
        Assert.That(headerRow, Is.Not.Null, "El header de columnas debe estar en la fila 5.");
        var cell0 = headerRow!.GetCell(0)?.ToString() ?? string.Empty;
        var cell1 = headerRow.GetCell(1)?.ToString() ?? string.Empty;
        var cell2 = headerRow.GetCell(2)?.ToString() ?? string.Empty;
        var cell3 = headerRow.GetCell(3)?.ToString() ?? string.Empty;
        Assert.That(cell0, Is.EqualTo("Especialidad"));
        Assert.That(cell1, Is.EqualTo("Nombre"));
        Assert.That(cell2, Is.EqualTo("UnidadMedida"));
        Assert.That(cell3, Is.EqualTo("Codigo"));

        // Sin datos: el helper emite solo el header (no hay filas de datos
        // ni fila de totales porque IncludeTotalsRow = false).
        Assert.That(sheet.LastRowNum, Is.EqualTo(5),
            "El sheet termina exactamente en la fila del header (sin filas de datos).");
    }

    [Test]
    public async Task DescargarPlantilla_SinFormato_DefaultEsXlsx()
    {
        var result = await _controller.DescargarPlantilla("material", formato: null, CancellationToken.None);

        var file = result as FileContentResult;
        Assert.That(file, Is.Not.Null);
        Assert.That(file!.FileDownloadName, Does.EndWith(".xlsx"));
    }

    [Test]
    public void DescargarPlantilla_FormatoTxt_LanzaFormatoPlantillaInvalidoException()
    {
        var ex = Assert.ThrowsAsync<FormatoPlantillaInvalidoException>(async () =>
            await _controller.DescargarPlantilla("material", "txt", CancellationToken.None))!;

        Assert.That(ex.FormatoRecibido, Is.EqualTo("txt"));
        Assert.That(ex.Message, Does.Contain("csv").And.Contain("xlsx"));
    }

    [Test]
    public void DescargarPlantilla_ModuloNoSoportado_LanzaPlantillaNoDisponibleException()
    {
        var ex = Assert.ThrowsAsync<PlantillaNoDisponibleException>(async () =>
            await _controller.DescargarPlantilla("unidad-medida", "xlsx", CancellationToken.None))!;

        Assert.That(ex.Modulo, Is.EqualTo("unidad-medida"));
    }

    [Test]
    public void DescargarPlantilla_ModuloCaseInsensitive_AceptaMaterial()
    {
        // "MATERIAL" y "Material" deben normalizarse y funcionar.
        Assert.DoesNotThrowAsync(async () =>
            await _controller.DescargarPlantilla("MATERIAL", "xlsx", CancellationToken.None));
    }

    [Test]
    public void DescargarPlantilla_FormatoCaseInsensitive_AceptaCSV()
    {
        Assert.DoesNotThrowAsync(async () =>
            await _controller.DescargarPlantilla("material", "CSV", CancellationToken.None));
    }

    // =========================================================================
    // Fake de IImportService
    //
    // Permite configurar el resultado o excepcion a devolver sin ejercitar
    // la logica real (parseo, mapeo, SP). El service real se prueba en
    // ImportServiceUnitTests; aca solo se verifica la capa HTTP.
    // =========================================================================

    private sealed class RecordingImportService : IImportService
    {
        public int Llamadas { get; private set; }
        public string? UltimoModulo { get; private set; }
        public IFormFile? UltimoArchivo { get; private set; }
        public string? UltimoUsuario { get; private set; }
        public CancellationToken UltimoCancellationToken { get; private set; }

        public ResultadoImportacion? ResultadoARetornar { get; set; }
        public Exception? ExcepcionALanzar { get; set; }

        public Task<ResultadoImportacion> ImportarAsync(
            string modulo, IFormFile archivo, string usuario, CancellationToken ct = default)
        {
            Llamadas++;
            UltimoModulo = modulo;
            UltimoArchivo = archivo;
            UltimoUsuario = usuario;
            UltimoCancellationToken = ct;

            if (ExcepcionALanzar is not null)
                throw ExcepcionALanzar;

            return Task.FromResult(ResultadoARetornar
                ?? new ResultadoImportacion(modulo, "csv", 0));
        }
    }
}
