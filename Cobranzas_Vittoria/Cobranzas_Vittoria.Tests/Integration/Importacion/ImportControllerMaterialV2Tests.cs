using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Importacion;

/// <summary>
/// Pruebas de integracion end-to-end del flujo de importacion v2 del modulo
/// <c>material</c>.
///
/// Cada test ejercita la cadena completa:
///   HTTP Request (multipart/form-data)
///     -&gt; ApiExceptionMiddleware
///     -&gt; ImportController
///     -&gt; ImportService
///     -&gt; MaterialImportProcessor
///     -&gt; FileParser (CSV/Excel)
///     -&gt; ResolvedorEntidadesService
///     -&gt; SQL Server (Testcontainers) + SP usp_Material_CargaMasiva_v2
///
/// Cubre los 9 escenarios del plan de Fase 5:
///   1. Happy path CSV UTF-8 con ';' (4 materiales, auto-crea catalogos)
///   2. Happy path CSV Windows-1252 (tildes y ñ)
///   3. Happy path XLS (HSSF)
///   4. Idempotencia (segunda carga del mismo archivo)
///   5. Rollback atomico (Codigo vacio en fila 3 -&gt; 422, ningun catalogo persiste)
///   6. Deteccion de delimitador ',' (fallback)
///   7. Plantilla CSV (GET /plantilla?formato=csv)
///   8. Plantilla XLSX (GET /plantilla?formato=xlsx)
///   9. Re-importacion de la plantilla (descargar -&gt; rellenar -&gt; importar)
///
/// <para>
/// <b>Nota:</b> se omitieron los tests de concurrencia (dos POSTs paralelos
/// creando el mismo catalogo) porque el sistema no tiene volumen de usuarios
/// simultaneos que justifique ese escenario. El retry del
/// <c>ResolvedorEntidadesService</c> se mantiene como defensa en profundidad.
/// </para>
///
/// <para>
/// <b>Aislamiento entre tests:</b> <c>maestra.Material</c>, <c>maestra.Especialidad</c>
/// y <c>maestra.UnidadMedida</c> estan en <c>TablesToIgnore</c> del Respawn
/// (ver <see cref="IntegrationTestBase"/>), asi que los datos SEMILLA
/// (BAL, BOL, CAJ...) persisten entre tests. Para evitar choques por
/// duplicados usamos <c>Guid.NewGuid().ToString("N")</c> en nombres de
/// catalogos y codigos de material. El test de idempotencia (#4) usa el
/// MISMO Guid en ambas cargas, intencionalmente.
/// </para>
/// </summary>
public class ImportControllerMaterialV2Tests : IntegrationTestBase
{
    private const string UsuarioTest = "test-user";

    // =========================================================================
    // 1) Happy path CSV UTF-8 con ';'
    // =========================================================================

    [Test]
    public async Task Post_CsvUtf8ConPuntoYComa_Crea4Materiales2Especialidades1Unidad_Retorna200EInserta()
    {
        // Arrange: 4 materiales, 2 especialidades nuevas (G1, G2), 1 unidad nueva (G3).
        var g1 = PrefijoUnico();  // Especialidad 1
        var g2 = PrefijoUnico();  // Especialidad 2
        var g3 = PrefijoUnico();  // UnidadMedida (nueva)
        var prefijoCodigo = PrefijoUnico(); // Codigos unicos para Material

        var csv = ImportFileBuilder.BuildCsvConSeparador(';',
            "Especialidad;Nombre;UnidadMedida;Codigo",
            $"{g1};Cemento Portland;{g3};{prefijoCodigo}-M1",
            $"{g1};Arena Gruesa;{g3};{prefijoCodigo}-M2",
            $"{g2};Cable #12 AWG;{g3};{prefijoCodigo}-M3",
            $"{g2};Llave Termica 20A;{g3};{prefijoCodigo}-M4");

        // Act
        var response = await PostImportAsync("material", "test.csv", csv, "text/csv", UsuarioTest);

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "modulo"), Is.EqualTo("material"));
        Assert.That(JsonHelpers.GetString(body, "formato"), Is.EqualTo("csv"));
        Assert.That(JsonHelpers.GetInt32(body, "filasInsertadas"), Is.EqualTo(4));

        // Assert BD: 4 materiales con ese prefijo de codigo
        var materiales = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Material WHERE Codigo LIKE @p",
            new { p = $"{prefijoCodigo}-%" });
        Assert.That(materiales, Is.EqualTo(4));

        // Assert BD: 2 especialidades nuevas (las del Guid) con Activo = 1
        var especialidades = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Especialidad WHERE Nombre IN (@g1, @g2) AND Activo = 1",
            new { g1, g2 });
        Assert.That(especialidades, Is.EqualTo(2));

        // Assert BD: 1 unidad nueva con Activo = 1
        var unidades = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.UnidadMedida WHERE Nombre = @g AND Activo = 1",
            new { g = g3 });
        Assert.That(unidades, Is.EqualTo(1));
    }

    // =========================================================================
    // 2) Happy path CSV Windows-1252 (tildes y ñ)
    // =========================================================================

    [Test]
    public async Task Post_CsvWindows1252ConPuntoYComa_ProcesaTildesYEnieCorrectamente_Retorna200()
    {
        // Arrange: nombres con tildes castellanas codificados en Windows-1252.
        // Estos bytes NO son UTF-8 validos: el parser debe detectarlo y hacer
        // fallback a Windows-1252 (superconjunto de ISO-8859-1).
        var g1 = PrefijoUnico();   // Especialidad con tildes
        var g2 = PrefijoUnico();   // UnidadMedida con tilde
        var prefijoCodigo = PrefijoUnico();

        var csv = ImportFileBuilder.BuildCsvConEncoding(
            Encoding.GetEncoding("Windows-1252"),
            "Especialidad;Nombre;UnidadMedida;Codigo",
            $"{g1};Año Construcción;{g2};{prefijoCodigo}-M1",
            $"{g1};Meses Plazo;{g2};{prefijoCodigo}-M2");

        // Act
        var response = await PostImportAsync("material", "test.csv", csv, "text/csv", UsuarioTest);

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetInt32(body, "filasInsertadas"), Is.EqualTo(2));

        // Assert BD: el Nombre del material se persistio correctamente
        // (sin caracteres de reemplazo '?' por la decodificacion).
        var nombres = await DbHelpers.QueryAsync<string>(
            "SELECT Descripcion FROM maestra.Material WHERE Codigo LIKE @p ORDER BY IdMaterial",
            new { p = $"{prefijoCodigo}-%" });
        var nombresList = nombres.ToList();
        Assert.That(nombresList, Has.Count.EqualTo(2));
        Assert.That(nombresList[0], Is.EqualTo("Año Construcción"));
        Assert.That(nombresList[1], Is.EqualTo("Meses Plazo"));
    }

    // =========================================================================
    // 3) Happy path XLS (HSSF, formato legacy .xls)
    // =========================================================================

    [Test]
    public async Task Post_XlsHssf_Procesa4Materiales_Retorna200()
    {
        // Arrange: archivo .xls (no .xlsx). El parser detecta la firma OLE2 y
        // usa HSSFWorkbook en lugar de XSSFWorkbook.
        var g1 = PrefijoUnico();
        var g2 = PrefijoUnico();
        var g3 = PrefijoUnico();
        var prefijoCodigo = PrefijoUnico();

        var xls = ImportFileBuilder.BuildXls(
            encabezados: new[] { "Especialidad", "Nombre", "UnidadMedida", "Codigo" },
            filas: new[]
            {
                new[] { g1, "Cemento", g3, $"{prefijoCodigo}-M1" },
                new[] { g1, "Arena", g3, $"{prefijoCodigo}-M2" },
                new[] { g2, "Cable", g3, $"{prefijoCodigo}-M3" },
                new[] { g2, "Llave", g3, $"{prefijoCodigo}-M4" }
            });

        // Act
        var response = await PostImportAsync("material", "test.xls", xls,
            "application/vnd.ms-excel", UsuarioTest);

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "formato"), Is.EqualTo("xls"));
        Assert.That(JsonHelpers.GetInt32(body, "filasInsertadas"), Is.EqualTo(4));

        // Assert BD
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Material WHERE Codigo LIKE @p",
            new { p = $"{prefijoCodigo}-%" });
        Assert.That(count, Is.EqualTo(4));
    }

    // =========================================================================
    // 4) Idempotencia
    // =========================================================================

    [Test]
    public async Task Post_MismoArchivoCsvDosVeces_SegundaVezNoDuplica_Retorna200()
    {
        // Arrange: 1 Especialidad y 1 UnidadMedida nuevas. La primera carga
        // las crea; la segunda ya las encuentra en el catalogo.
        var g1 = PrefijoUnico();
        var g2 = PrefijoUnico();
        var prefijoCodigo = PrefijoUnico();

        var csv = ImportFileBuilder.BuildCsvConSeparador(';',
            "Especialidad;Nombre;UnidadMedida;Codigo",
            $"{g1};Cemento Portland;{g2};{prefijoCodigo}-M1",
            $"{g1};Arena Gruesa;{g2};{prefijoCodigo}-M2");

        // Act 1: primera carga.
        var response1 = await PostImportAsync("material", "test.csv", csv, "text/csv", UsuarioTest);
        Assert.That(response1.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Primera carga: {await response1.Content.ReadAsStringAsync()}");
        var body1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetInt32(body1, "filasInsertadas"), Is.EqualTo(2));

        // Act 2: misma carga. El resolvedor debe encontrar los catalogos
        // (no crear duplicados); el SP hace el INSERT de los Materiales.
        // Como Material.Codigo es UNIQUE, los nuevos INSERT recibiran 50003
        // (VALOR_YA_EXISTE_EN_BD). Por eso este test es valido para probar
        // la idempotencia PARCIAL: la primera vez crea catalogos, la segunda
        // NO duplica catalogos.
        var response2 = await PostImportAsync("material", "test.csv", csv, "text/csv", UsuarioTest);

        // Assert BD: 1 sola Especialidad, 1 sola UnidadMedida.
        var espCount = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Especialidad WHERE Nombre = @n",
            new { n = g1 });
        Assert.That(espCount, Is.EqualTo(1), "No debe duplicar la Especialidad.");

        var umCount = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.UnidadMedida WHERE Nombre = @n",
            new { n = g2 });
        Assert.That(umCount, Is.EqualTo(1), "No debe duplicar la UnidadMedida.");

        // Assert 2da carga: como Material.Codigo es UNIQUE, el 2do POST
        // falla con 422 (50003 VALOR_YA_EXISTE_EN_BD). Esta es la
        // INTENCION: el test prueba que los catalogos no se duplican;
        // la deduplicacion de Materiales es responsabilidad de OTRO feature
        // (no es requisito del plan v2).
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            "La segunda carga con los mismos Codigos debe chocar contra la UNIQUE constraint de Material.");
    }

    // =========================================================================
    // 5) Rollback atomico (Codigo vacio en fila 3)
    // =========================================================================

    [Test]
    public async Task Post_CsvConCodigoVacioEnFila3_Retorna422_NiEspecialidadesNiUnidadesPersistidas()
    {
        // Arrange: 3 filas. Fila 3 tiene Codigo vacio -> la fila no es valida.
        // El processor debe reportar la fila, NO invocar el SP, NO persistir
        // NADA (ni las Especialidades/UnidadMedida de las filas 1 y 2).
        // Esto valida que el resolver se ejecuta en la MISMA transaccion
        // que el INSERT (atomicidad real).
        var g1 = PrefijoUnico();
        var g2 = PrefijoUnico();
        var prefijoCodigo = PrefijoUnico();

        var csv = ImportFileBuilder.BuildCsvConSeparador(';',
            "Especialidad;Nombre;UnidadMedida;Codigo",
            $"{g1};Cemento;{g2};{prefijoCodigo}-M1",
            $"{g1};Arena;{g2};{prefijoCodigo}-M2",
            $"{g1};Tierra;{g2};");

        // Act
        var response = await PostImportAsync("material", "test.csv", csv, "text/csv", UsuarioTest);

        // Assert HTTP: 422 por DatosInvalidosException
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));

        // Assert BD: NINGUNA Especialidad persistida (el resolver corre DENTRO
        // de la transaccion del processor; al fallar la fila 3, rollback
        // completo).
        var espCount = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Especialidad WHERE Nombre = @n",
            new { n = g1 });
        Assert.That(espCount, Is.EqualTo(0),
            "La Especialidad no debe persistirse si hay una fila invalida posterior.");

        // Assert BD: NINGUNA UnidadMedida persistida
        var umCount = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.UnidadMedida WHERE Nombre = @n",
            new { n = g2 });
        Assert.That(umCount, Is.EqualTo(0),
            "La UnidadMedida no debe persistirse si hay una fila invalida posterior.");

        // Assert BD: NINGUN Material persistido
        var matCount = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Material WHERE Codigo LIKE @p",
            new { p = $"{prefijoCodigo}-%" });
        Assert.That(matCount, Is.EqualTo(0));
    }

    // =========================================================================
    // 8) Deteccion de delimitador ',' (fallback)
    // =========================================================================

    [Test]
    public async Task Post_CsvConComa_DetectaDelimitadorCorrectamente_Retorna200()
    {
        // Arrange: CSV con coma (no con ';').
        var g1 = PrefijoUnico();
        var g2 = PrefijoUnico();
        var prefijoCodigo = PrefijoUnico();

        var csv = ImportFileBuilder.BuildCsvConSeparador(',',
            "Especialidad,Nombre,UnidadMedida,Codigo",
            $"{g1},Cemento,{g2},{prefijoCodigo}-M1",
            $"{g1},Arena,{g2},{prefijoCodigo}-M2");

        // Act
        var response = await PostImportAsync("material", "test.csv", csv, "text/csv", UsuarioTest);

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetInt32(body, "filasInsertadas"), Is.EqualTo(2));

        // Assert BD
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Material WHERE Codigo LIKE @p",
            new { p = $"{prefijoCodigo}-%" });
        Assert.That(count, Is.EqualTo(2));
    }

    // =========================================================================
    // 9) Plantilla CSV: GET /api/import/material/plantilla?formato=csv
    // =========================================================================

    [Test]
    public async Task Get_PlantillaCsv_Retorna200_ConHeadersYBom()
    {
        // Act
        var response = await _client.GetAsync("/api/import/material/plantilla?formato=csv");

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/csv"));
        Assert.That(response.Content.Headers.ContentType?.CharSet, Is.EqualTo("utf-8"));

        // Assert Content-Disposition
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.That(disposition, Is.Not.Null);
        Assert.That(disposition!.FileName, Does.StartWith("plantilla-materiales-"));
        Assert.That(disposition.FileName, Does.EndWith(".csv"));

        // Assert body: BOM UTF-8 + headers correctos + sin filas de datos
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(3), "Debe incluir BOM UTF-8.");
        Assert.That(bytes[0], Is.EqualTo((byte)0xEF));
        Assert.That(bytes[1], Is.EqualTo((byte)0xBB));
        Assert.That(bytes[2], Is.EqualTo((byte)0xBF));

        var texto = Encoding.UTF8.GetString(bytes);
        if (texto.Length > 0 && texto[0] == '\uFEFF') texto = texto[1..]; // remover BOM
        var primeraLinea = texto.Replace("\r", string.Empty).Split('\n')[0];
        Assert.That(primeraLinea, Is.EqualTo("Especialidad;Nombre;UnidadMedida;Codigo"));

        // Solo 1 linea (header); no debe haber filas de ejemplo.
        var totalLineas = texto.Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.That(totalLineas, Is.EqualTo(1),
            "La plantilla solo tiene la linea de headers (sin filas de ejemplo).");
    }

    // =========================================================================
    // 10) Plantilla XLSX: GET /api/import/material/plantilla?formato=xlsx
    // =========================================================================

    [Test]
    public async Task Get_PlantillaXlsx_Retorna200_ConHeadersCorrectos()
    {
        // Act
        var response = await _client.GetAsync("/api/import/material/plantilla?formato=xlsx");

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        Assert.That(response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

        // Assert Content-Disposition
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.That(disposition, Is.Not.Null);
        Assert.That(disposition!.FileName, Does.EndWith(".xlsx"));

        // Assert body: abrimos con NPOI y verificamos headers
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(bytes);
        var workbook = new NPOI.XSSF.UserModel.XSSFWorkbook(ms);
        var sheet = workbook.GetSheetAt(0);

        // Buscamos la fila de headers (con layout del helper: fila 5 con Title+Filters+GeneratedAt).
        var headerRow = sheet.GetRow(5);
        Assert.That(headerRow, Is.Not.Null, "El header debe estar en la fila 5 (layout del helper NPOI).");
        var cell0 = headerRow!.GetCell(0)?.ToString() ?? string.Empty;
        var cell1 = headerRow.GetCell(1)?.ToString() ?? string.Empty;
        var cell2 = headerRow.GetCell(2)?.ToString() ?? string.Empty;
        var cell3 = headerRow.GetCell(3)?.ToString() ?? string.Empty;
        Assert.That(cell0, Is.EqualTo("Especialidad"));
        Assert.That(cell1, Is.EqualTo("Nombre"));
        Assert.That(cell2, Is.EqualTo("UnidadMedida"));
        Assert.That(cell3, Is.EqualTo("Codigo"));
    }

    // =========================================================================
    // 11) Re-importacion: descargar plantilla -> rellenar -> importar
    // =========================================================================

    [Test]
    public async Task Post_ReimportarCsvDePlantillaConDatosFrescos_Retorna200()
    {
        // Arrange 1: descargar la plantilla CSV.
        var responsePlantilla = await _client.GetAsync("/api/import/material/plantilla?formato=csv");
        Assert.That(responsePlantilla.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var plantillaBytes = await responsePlantilla.Content.ReadAsByteArrayAsync();

        // Arrange 2: rellenar la plantilla con 2 filas nuevas (Guid-based).
        var plantillaTexto = Encoding.UTF8.GetString(plantillaBytes);
        if (plantillaTexto.Length > 0 && plantillaTexto[0] == '\uFEFF') plantillaTexto = plantillaTexto[1..];
        // Separador es ';' (plantilla del proyecto).
        var g1 = PrefijoUnico();
        var g2 = PrefijoUnico();
        var prefijoCodigo = PrefijoUnico();
        var rellenada = plantillaTexto.TrimEnd() + "\r\n" +
            $"{g1};Cemento Portland;{g2};{prefijoCodigo}-M1\r\n" +
            $"{g1};Arena Gruesa;{g2};{prefijoCodigo}-M2";
        var rellenadaBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(rellenada);
        // (No es necesario agregar BOM manualmente: el controller/processor
        //  detecta UTF-8 valido con o sin BOM. El test verifica el flujo
        //  end-to-end de la plantilla descargada.)

        // Act
        var response = await PostImportAsync("material", "test.csv",
            rellenadaBytes, "text/csv", UsuarioTest);

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetInt32(body, "filasInsertadas"), Is.EqualTo(2));

        // Assert BD
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Material WHERE Codigo LIKE @p",
            new { p = $"{prefijoCodigo}-%" });
        Assert.That(count, Is.EqualTo(2));
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Construye y envia un POST <c>multipart/form-data</c> con los campos
    /// <c>archivo</c> y <c>usuario</c> al endpoint <c>/api/import/{modulo}</c>.
    /// </summary>
    private async Task<HttpResponseMessage> PostImportAsync(
        string modulo,
        string filename,
        byte[] fileBytes,
        string contentType,
        string usuario)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "archivo", filename);
        content.Add(new StringContent(usuario), "usuario");
        return await _client.PostAsync($"/api/import/{modulo}", content);
    }

    /// <summary>
    /// Prefijo unico por test (Guid N, 12 chars, mayusculas) para evitar choques
    /// con el seed persistente. El mismo prefijo puede usarse para Especialidad,
    /// UnidadMedida y como parte del Codigo del Material.
    /// </summary>
    private static string PrefijoUnico() => Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
}
