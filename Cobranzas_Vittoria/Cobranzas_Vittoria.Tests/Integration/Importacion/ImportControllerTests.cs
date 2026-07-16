using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Importacion;

/// <summary>
/// Pruebas de integracion end-to-end del <c>ImportController</c>.
///
/// Cubre el flujo HTTP completo:
///   HTTP Request (multipart/form-data)
///     -&gt; ApiExceptionMiddleware
///     -&gt; ImportController
///     -&gt; ImportService
///     -&gt; ImportProcessorBase
///     -&gt; FileParser (CSV/Excel)
///     -&gt; FileValidator
///     -&gt; ImportRepository
///     -&gt; SQL Server (Testcontainers)
///
/// Cada test:
///   1. Construye el body multipart con <see cref="ImportFileBuilder"/>.
///   2. POST a <c>/api/import/{modulo}</c>.
///   3. Verifica el HTTP status code Y el JSON body (codigo + errores).
///   4. Para happy paths, verifica ademas que las filas quedaron en BD.
///
/// <para>
/// <b>Persistencia:</b> la BD se resetea en cada test por
/// <see cref="IntegrationTestBase"/> (Respawn). Las tablas semilla
/// (<c>UnidadMedida</c>, <c>Especialidad</c>, etc.) estan en
/// <c>TablesToIgnore</c>, por lo que los codigos del seed (UM-001, BAL, BOL...)
/// persisten y se usan para verificar colisiones en los tests de error.
/// </para>
/// </summary>
public class ImportControllerTests : IntegrationTestBase
{
    private const string UsuarioTest = "test-user";

    // =========================================================================
    // Happy paths
    // =========================================================================

    [Test]
    public async Task Post_CsvValido5FilasUnidadMedida_Retorna200EInsertaEnBd()
    {
        // Arrange
        var prefijo = PrefijoUnico();
        var csv = ImportFileBuilder.BuildCsv(
            "Codigo,Nombre",
            $"{prefijo}-001,Unidad 1",
            $"{prefijo}-002,Unidad 2",
            $"{prefijo}-003,Unidad 3",
            $"{prefijo}-004,Unidad 4",
            $"{prefijo}-005,Unidad 5");

        // Act
        var response = await PostImportAsync("unidad-medida", "test.csv", csv, "text/csv", UsuarioTest);

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "modulo"), Is.EqualTo("unidad-medida"));
        Assert.That(JsonHelpers.GetString(body, "formato"), Is.EqualTo("csv"));
        Assert.That(JsonHelpers.GetInt32(body, "filasInsertadas"), Is.EqualTo(5));

        // Assert BD
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.UnidadMedida WHERE Codigo LIKE @p",
            new { p = $"{prefijo}-%" });
        Assert.That(count, Is.EqualTo(5));
    }

    [Test]
    public async Task Post_XlsxValido3FilasEspecialidad_Retorna200EInsertaEnBd()
    {
        // Arrange
        var prefijo = PrefijoUnico();
        var xlsx = ImportFileBuilder.BuildXlsx(
            encabezados: new[] { "Nombre", "Descripcion", "Activo" },
            filas: new[]
            {
                new[] { $"{prefijo}-1", "Desc 1", "true" },
                new[] { $"{prefijo}-2", "Desc 2", "true" },
                new[] { $"{prefijo}-3", "Desc 3", "false" }
            });

        // Act
        var response = await PostImportAsync("especialidad", "test.xlsx", xlsx,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", UsuarioTest);

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "modulo"), Is.EqualTo("especialidad"));
        Assert.That(JsonHelpers.GetInt32(body, "filasInsertadas"), Is.EqualTo(3));

        // Assert BD
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Especialidad WHERE Nombre LIKE @p",
            new { p = $"{prefijo}-%" });
        Assert.That(count, Is.EqualTo(3));
    }

    // =========================================================================
    // Errores de archivo
    // =========================================================================

    [Test]
    public async Task Post_SinArchivo_Retorna400PorModelBinding()
    {
        // Arrange: solo el campo usuario, sin "archivo".
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(UsuarioTest), "usuario");

        // Act
        var response = await _client.PostAsync("/api/import/unidad-medida", content);

        // Assert
        // [ApiController] retorna 400 automaticamente cuando [FromForm] IFormFile
        // no se envia (ModelState.IsValid = false).
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            $"Body: {await response.Content.ReadAsStringAsync()}");
    }

    [Test]
    public async Task Post_ExtensionTxt_Retorna400ExtensionInvalida()
    {
        // Arrange
        var txt = ImportFileBuilder.BuildTxt("Codigo,Nombre", "C-001,Test");
        var response = await PostImportAsync("unidad-medida", "datos.txt", txt, "text/plain", UsuarioTest);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("EXTENSION_INVALIDA"));
    }

    [Test]
    public async Task Post_ArchivoMayor10Mb_Retorna413TamanioExcedido()
    {
        // Arrange: 10 MB + 1 byte (pasa RequestSizeLimit de 11MB, falla FileValidator).
        var csvGrande = ImportFileBuilder.BuildCsvGrande(cantidadFilas: 60_000, anchoFila: 200);
        Assert.That(csvGrande.Length, Is.GreaterThan(10 * 1024 * 1024), "El archivo debe superar 10MB.");

        // Act
        var response = await PostImportAsync("unidad-medida", "grande.csv", csvGrande, "text/csv", UsuarioTest);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("TAMANIO_EXCEDIDO"));
    }

    // =========================================================================
    // Errores de estructura
    // =========================================================================

    [Test]
    public async Task Post_SinColumnasRequeridas_Retorna400EncabezadosIncorrectos()
    {
        // Arrange: la primera fila tiene "Foo,Bar" en vez de "Codigo,Nombre".
        var csv = ImportFileBuilder.BuildCsv(
            "Foo,Bar",
            "C-001,Test");

        // Act
        var response = await PostImportAsync("unidad-medida", "test.csv", csv, "text/csv", UsuarioTest);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("ENCABEZADOS_INCORRECTOS"));
    }

    [Test]
    public async Task Post_SoloEncabezadosSinFilas_Retorna422ArchivoSinDatos()
    {
        // Arrange: solo "Codigo,Nombre" sin filas debajo.
        var csv = ImportFileBuilder.BuildCsv("Codigo,Nombre");

        // Act
        var response = await PostImportAsync("unidad-medida", "vacio.csv",
            csv, "text/csv", UsuarioTest);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));
    }

    // =========================================================================
    // Errores de datos
    // =========================================================================

    [Test]
    public async Task Post_FilaConCodigoVacio_Retorna422ConErrorPorFila()
    {
        // Arrange: el parser numera las filas de datos a partir de 1
        // (la fila de encabezados NO cuenta). Por lo tanto:
        //   - linea 1 del archivo = header (Codigo,Nombre)
        //   - linea 2 = fila 1 de datos (valida)
        //   - linea 3 = fila 2 de datos (Codigo vacio -> error)
        //   - linea 4 = fila 3 de datos (valida)
        var prefijo = PrefijoUnico();
        var csv = ImportFileBuilder.BuildCsv(
            "Codigo,Nombre",
            $"{prefijo}-001,Valido 1",
            ",Codigo Vacio",                  // <- Codigo vacio, parser lo expone como null
            $"{prefijo}-003,Valido 3");

        // Act
        var response = await PostImportAsync("unidad-medida", "test.csv", csv, "text/csv", UsuarioTest);

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));

        // Assert errores: solo la fila 2 (segunda linea de datos) debe estar reportada.
        var errores = body.GetProperty("errores");
        Assert.That(errores.GetArrayLength(), Is.EqualTo(1));
        var error = errores[0];
        Assert.That(error.GetProperty("fila").GetInt32(), Is.EqualTo(2));
        Assert.That(error.GetProperty("codigoError").GetString(), Is.EqualTo("CAMPO_REQUERIDO"));

        // Assert BD: ninguna fila se inserto (rollback).
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.UnidadMedida WHERE Codigo LIKE @p",
            new { p = $"{prefijo}-%" });
        Assert.That(count, Is.EqualTo(0), "El rollback debe haber limpiado el intento de insert.");
    }

    [Test]
    public async Task Post_CodigosDuplicadosEnArchivo_Retorna422ValorDuplicadoEnArchivo()
    {
        // Arrange: dos filas con el mismo Codigo.
        var prefijo = PrefijoUnico();
        var codigoDuplicado = $"{prefijo}-DUP";
        var csv = ImportFileBuilder.BuildCsv(
            "Codigo,Nombre",
            $"{codigoDuplicado},Nombre 1",
            $"{codigoDuplicado},Nombre 2");

        // Act
        var response = await PostImportAsync("unidad-medida", "dup.csv",
            csv, "text/csv", UsuarioTest);

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));

        // El error viene del SP (codigo 50002), no de la validacion por fila.
        var errores = body.GetProperty("errores");
        Assert.That(errores.GetArrayLength(), Is.GreaterThan(0));
        Assert.That(errores[0].GetProperty("codigoError").GetString(),
            Is.EqualTo("VALOR_DUPLICADO_EN_ARCHIVO"));
    }

    [Test]
    public async Task Post_CodigosExistentesEnBD_Retorna422ValorYaExisteEnBd()
    {
        // Arrange: la BD tiene el codigo "BAL" del seed; intentamos re-importarlo.
        // (La tabla maestra.UnidadMedida esta en TablesToIgnore del Respawn, por lo
        // que los datos seed persisten entre tests.)
        var csv = ImportFileBuilder.BuildCsv(
            "Codigo,Nombre",
            "BAL,Re-importar Bal");

        // Act
        var response = await PostImportAsync("unidad-medida", "reimportar.csv",
            csv, "text/csv", UsuarioTest);

        // Assert HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));

        var errores = body.GetProperty("errores");
        Assert.That(errores[0].GetProperty("codigoError").GetString(),
            Is.EqualTo("VALOR_YA_EXISTE_EN_BD"));
    }

    // =========================================================================
    // Errores de modulo
    // =========================================================================

    [Test]
    public async Task Post_ModuloInexistente_Retorna400ModuloNoSoportado()
    {
        // Arrange
        var csv = ImportFileBuilder.BuildCsv("Codigo,Nombre", "C-001,Test");

        // Act
        var response = await PostImportAsync("modulo-inexistente", "test.csv",
            csv, "text/csv", UsuarioTest);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("MODULO_NO_SOPORTADO"));
        Assert.That(JsonHelpers.GetString(body, "message"), Does.Contain("modulo-inexistente"));
        // El mensaje debe listar los modulos disponibles para ayudar al cliente.
        Assert.That(JsonHelpers.GetString(body, "message"), Does.Contain("unidad-medida"));
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Construye y envia un POST <c>multipart/form-data</c> con los campos
    /// <c>archivo</c> y <c>usuario</c> al endpoint <c>/api/import/{modulo}</c>.
    /// </summary>
    /// <remarks>
    /// <b>Por que <c>async Task</c> y no <c>Task</c>:</b> si el metodo fuera
    /// no-async y usara <c>using var content</c>, el <c>using</c> disposearia
    /// el <c>MultipartFormDataContent</c> (y su <c>ByteArrayContent</c>
    /// interno con su <c>MemoryStream</c>) al salir del metodo, ANTES de que
    /// <c>PostAsync</c> termine de leer el body. Eso lanzaria
    /// <c>ObjectDisposedException</c> en el lado del HttpClient. Al ser
    /// <c>async</c>, el <c>using</c> se ejecuta despues del <c>await</c>.
    /// </remarks>
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
        // El nombre "archivo" debe coincidir con el parametro del controller
        // ([FromForm] IFormFile archivo).
        content.Add(fileContent, "archivo", filename);
        content.Add(new StringContent(usuario), "usuario");

        return await _client.PostAsync($"/api/import/{modulo}", content);
    }

    /// <summary>
    /// Genera un prefijo unico por test para evitar choques con datos del seed
    /// y con filas residuales del Respawn. Usa Guid para garantizar unicidad
    /// incluso si el test se ejecuta varias veces seguidas.
    /// </summary>
    private static string PrefijoUnico() => $"TST-{Guid.NewGuid():N}".Substring(0, 16).ToUpper();
}
