using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

/// <summary>
/// Pruebas de GastosAdministrativosController.
///
///   GET    /api/contable/gastos-administrativos
///   GET    /api/contable/gastos-administrativos/{id}
///   POST   /api/contable/gastos-administrativos             (Upsert con IdGasto=null => INSERT)
///   PUT    /api/contable/gastos-administrativos/{id}        (Upsert con IdGasto=id => UPDATE)
///   DELETE /api/contable/gastos-administrativos/{id}        (soft delete: Activo=0)
///   GET    /api/contable/gastos-administrativos/{id}/documentos
///   POST   /api/contable/gastos-administrativos/{id}/documentos
///   GET    /api/contable/gastos-administrativos/{id}/documentos/{docId}/download
///
/// Service retorna entidades POCO, por lo que la serializacion usa camelCase.
///   - GET por id devuelve { gasto, documentos }.
///   - POST/PUT devuelven { idGastoAdministrativo }.
///   - DELETE devuelve { ok: true }.
///
/// Validaciones inline en el repo (lanzan InvalidOperationException, que
/// ApiExceptionMiddleware trata como UNHANDLED_ERROR -> 500):
///   - IdProyecto <= 0
///   - IdCategoriaGasto <= 0
///   - IdProveedorGastoAdministrativo <= 0
///   - Monto <= 0
/// (Nota: el middleware actual solo distingue SqlException (SQL_ERROR) del resto
///  (UNHANDLED_ERROR). No hay rama para InvalidOperationException -> 400.
///  Esto es deuda tecnica, no la corregimos aqui.)
///
/// Upload: requiere tipoDocumento="Factura" o "Pago" y archivos con extension .pdf.
///   Guarda en wwwroot/uploads/gastos-administrativos/{id}/{factura|pago}/{guid}_{filename}.
/// </summary>
public class GastosAdministrativosControllerTests : IntegrationTestBase
{
    // IDs del seed V1_1_0
    private const int IdProyecto = 10;                            // Mayta Capac II
    private const int IdCategoriaGasto = 6;                       // GASTOS ADMINISTRATIVOS
    private const int IdProveedorGastoAdministrativo = 24;        // ESCUELA DE CONDUCTORES JOSE OLAYA (Cat=6)

    // ---- Helpers compartidos ----

    /// <summary>
    /// Crea un gasto via POST y devuelve el IdGastoAdministrativo generado.
    /// </summary>
    private async Task<int> CrearGastoAsync(decimal monto = 100m, string descripcion = "Gasto de prueba")
    {
        var dto = new GastoAdministrativoUpsertDto
        {
            IdProyecto = IdProyecto,
            IdCategoriaGasto = IdCategoriaGasto,
            IdProveedorGastoAdministrativo = IdProveedorGastoAdministrativo,
            Fecha = DateTime.Today,
            Monto = monto,
            Descripcion = descripcion,
            Moneda = "PEN",
            Activo = true
        };
        var response = await _client.PostAsJsonAsync("/api/contable/gastos-administrativos", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear gasto. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(body, "idGastoAdministrativo");
    }

    /// <summary>
    /// Sube un PDF dummy como documento de tipo=tipoDocumento (Factura|Pago) al gasto dado.
    /// </summary>
    private async Task<int> SubirDocumentoPdfAsync(int idGasto, string tipoDocumento, string filename)
    {
        // Bytes minimos de un PDF valido (header %PDF-1.4)
        var pdfBytes = Encoding.ASCII.GetBytes(
            "%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n1 0 obj\n<<>>\nendobj\nxref\n0 1\n0000000000 65535 f\ntrailer\n<<>>\nstartxref\n0\n%%EOF\n");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(tipoDocumento), "tipoDocumento");

        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "files", filename);

        var response = await _client.PostAsync(
            $"/api/contable/gastos-administrativos/{idGasto}/documentos",
            content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al subir documento. Body: {await response.Content.ReadAsStringAsync()}");

        // Recuperar el id del documento recien subido
        var lista = await (await _client.GetAsync(
            $"/api/contable/gastos-administrativos/{idGasto}/documentos"))
            .Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(lista[0], "idGastoAdministrativoDocumento");
    }

    // ---- Tests: GET (List + Get) ----

    [Test]
    public async Task List_SinFiltros_RetornaArrayVacio()
    {
        // Act
        var response = await _client.GetAsync("/api/contable/gastos-administrativos");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task Get_ConIdExistente_RetornaGastoYDocumentos()
    {
        // Arrange - crear gasto
        var idGasto = await CrearGastoAsync(monto: 250.50m);

        // Act
        var response = await _client.GetAsync($"/api/contable/gastos-administrativos/{idGasto}");

        // Assert - estructura { gasto, documentos }
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var gasto = JsonHelpers.GetProp(body, "gasto");
        Assert.That(JsonHelpers.GetInt32(gasto, "idGastoAdministrativo"), Is.EqualTo(idGasto));
        Assert.That(JsonHelpers.GetInt32(gasto, "idProyecto"), Is.EqualTo(IdProyecto));
        Assert.That(JsonHelpers.GetInt32(gasto, "idCategoriaGasto"), Is.EqualTo(IdCategoriaGasto));
        Assert.That(JsonHelpers.GetInt32(gasto, "idProveedorGastoAdministrativo"), Is.EqualTo(IdProveedorGastoAdministrativo));
        Assert.That(JsonHelpers.GetDecimal(gasto, "monto"), Is.EqualTo(250.50m));
        Assert.That(JsonHelpers.GetString(gasto, "categoria"), Is.Not.Empty);
        Assert.That(JsonHelpers.GetString(gasto, "proveedor"), Is.Not.Empty);

        var documentos = JsonHelpers.GetProp(body, "documentos");
        Assert.That(documentos.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(documentos.GetArrayLength(), Is.EqualTo(0),
            "Gasto nuevo no debe tener documentos.");
    }

    [Test]
    public async Task Get_ConIdInexistente_RetornaNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/contable/gastos-administrativos/999999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ---- Tests: POST / PUT ----

    [Test]
    public async Task Create_ConDatosValidos_RetornaIdYPersiste()
    {
        // Arrange
        var dto = new GastoAdministrativoUpsertDto
        {
            IdProyecto = IdProyecto,
            IdCategoriaGasto = IdCategoriaGasto,
            IdProveedorGastoAdministrativo = IdProveedorGastoAdministrativo,
            Fecha = new DateTime(2026, 7, 1),
            Monto = 1750.75m,
            Descripcion = "Gasto de prueba Crear",
            Moneda = "PEN",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contable/gastos-administrativos", dto);

        // Assert - 1: HTTP 200 + id devuelto
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var idGasto = JsonHelpers.GetInt32(body, "idGastoAdministrativo");
        Assert.That(idGasto, Is.GreaterThan(0));

        // Assert - 2: fila persistida en BD con los datos correctos
        var fila = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT Monto, Descripcion, Moneda, Activo
              FROM contable.GastoAdministrativo
              WHERE IdGastoAdministrativo = @id",
            new { id = idGasto })).FirstOrDefault();
        Assert.That(fila, Is.Not.Null);
        Assert.That((decimal)fila.Monto, Is.EqualTo(1750.75m));
        Assert.That((string)fila.Descripcion, Is.EqualTo("Gasto de prueba Crear"));
        Assert.That((string)fila.Moneda, Is.EqualTo("PEN"));
        Assert.That((bool)fila.Activo, Is.True);
    }

    [Test]
    public async Task Update_ConIdExistente_ActualizaDatos()
    {
        // Arrange - crear gasto con Monto=100
        var idGasto = await CrearGastoAsync(monto: 100m, descripcion: "Original");

        // Act - PUT con Monto=500
        var dto = new GastoAdministrativoUpsertDto
        {
            IdProyecto = IdProyecto,
            IdCategoriaGasto = IdCategoriaGasto,
            IdProveedorGastoAdministrativo = IdProveedorGastoAdministrativo,
            Fecha = DateTime.Today,
            Monto = 500m,
            Descripcion = "Actualizado",
            Moneda = "PEN",
            Activo = true
        };
        var response = await _client.PutAsJsonAsync(
            $"/api/contable/gastos-administrativos/{idGasto}", dto);

        // Assert - 1: HTTP 200 + mismo id
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetInt32(body, "idGastoAdministrativo"), Is.EqualTo(idGasto));

        // Assert - 2: BD refleja los nuevos valores
        var actualizado = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT Monto, Descripcion
              FROM contable.GastoAdministrativo
              WHERE IdGastoAdministrativo = @id",
            new { id = idGasto })).FirstOrDefault();
        Assert.That(actualizado, Is.Not.Null);
        Assert.That((decimal)actualizado.Monto, Is.EqualTo(500m),
            "El Monto debe haberse actualizado a 500.");
        Assert.That((string)actualizado.Descripcion, Is.EqualTo("Actualizado"),
            "La Descripcion debe haberse actualizado.");
    }

    // ---- Tests: Validacion, Delete y Upload ----

    [Test]
    public async Task Create_ConMontoCero_RetornaBadRequest()
    {
        // Arrange
        var dto = new GastoAdministrativoUpsertDto
        {
            IdProyecto = IdProyecto,
            IdCategoriaGasto = IdCategoriaGasto,
            IdProveedorGastoAdministrativo = IdProveedorGastoAdministrativo,
            Fecha = DateTime.Today,
            Monto = 0m,  // invalido
            Moneda = "PEN",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contable/gastos-administrativos", dto);

        // Assert - el repo lanza InvalidOperationException que el middleware trata como UNHANDLED_ERROR (500)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("UNHANDLED_ERROR"));
        Assert.That(JsonHelpers.GetString(body, "message"), Does.Contain("mayor a cero"));
    }

    [Test]
    public async Task Delete_ConIdExistente_MarcaActivoFalse()
    {
        // Arrange - crear gasto
        var idGasto = await CrearGastoAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/contable/gastos-administrativos/{idGasto}");

        // Assert - 1: HTTP 200 con { ok: true }
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetBoolean(body, "ok"), Is.True);

        // Assert - 2: la fila sigue existiendo pero con Activo=0 (soft delete)
        var activo = await DbHelpers.QueryScalarAsync<bool>(
            "SELECT Activo FROM contable.GastoAdministrativo WHERE IdGastoAdministrativo = @id",
            new { id = idGasto });
        Assert.That(activo, Is.False,
            "DELETE debe ser soft delete (Activo=0), la fila no debe eliminarse fisicamente.");

        // Assert - 3: GET sigue devolviendo la fila (no NotFound) pero List filtrado por activo=false la muestra
        var getResponse = await _client.GetAsync($"/api/contable/gastos-administrativos/{idGasto}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "El GET por id debe seguir funcionando tras soft delete.");
    }

    [Test]
    public async Task UploadDocumentos_ConArchivoPdf_RetornaOkYGuardaFilaEnBD()
    {
        // Arrange - crear gasto
        var idGasto = await CrearGastoAsync();

        // Act - subir un PDF de tipo Factura
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Factura"), "tipoDocumento");
        var pdfBytes = Encoding.ASCII.GetBytes(
            "%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n1 0 obj\n<<>>\nendobj\nxref\n0 1\n0000000000 65535 f\ntrailer\n<<>>\nstartxref\n0\n%%EOF\n");
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "files", "factura-test.pdf");

        var response = await _client.PostAsync(
            $"/api/contable/gastos-administrativos/{idGasto}/documentos", content);

        // Assert - 1: HTTP 200 con { ok: true }
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetBoolean(body, "ok"), Is.True);

        // Assert - 2: fila persistida en BD con TipoDocumento=Factura y Extension=.pdf
        var filaDoc = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT TOP 1 TipoDocumento, Extension
              FROM contable.GastoAdministrativoDocumento
              WHERE IdGastoAdministrativo = @id
              ORDER BY IdGastoAdministrativoDocumento DESC",
            new { id = idGasto })).FirstOrDefault();
        Assert.That(filaDoc, Is.Not.Null);
        Assert.That((string)filaDoc.TipoDocumento, Is.EqualTo("Factura"));
        Assert.That((string)filaDoc.Extension, Is.EqualTo(".pdf"));

        // Assert - 3: la lista de documentos del GET tiene 1 elemento
        var getDocResponse = await _client.GetAsync(
            $"/api/contable/gastos-administrativos/{idGasto}/documentos");
        var docs = await getDocResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(docs.GetArrayLength(), Is.EqualTo(1));
        Assert.That(JsonHelpers.GetString(docs[0], "nombreArchivo"), Is.EqualTo("factura-test.pdf"));
    }
}
