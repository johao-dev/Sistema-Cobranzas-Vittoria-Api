using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

/// <summary>
/// Pruebas de GastosProyectoController.
///
/// TODOS los endpoints llevan {tipoModulo} en la ruta, ej:
///   /api/contable/gastos-proyecto/Terreno/{gastoId}
///
///   GET    /api/contable/gastos-proyecto/{tipoModulo}?idProyecto=&concepto=&estado=&activo=
///   GET    /api/contable/gastos-proyecto/{tipoModulo}/{gastoId}
///   POST   /api/contable/gastos-proyecto/{tipoModulo}                  (Upsert con IdGasto=null => INSERT)
///   PUT    /api/contable/gastos-proyecto/{tipoModulo}/{gastoId}         (Upsert con IdGasto=id => UPDATE)
///   DELETE /api/contable/gastos-proyecto/{tipoModulo}/{gastoId}         (soft delete: Activo=0)
///   GET    /api/contable/gastos-proyecto/{tipoModulo}/{gastoId}/documentos
///   POST   /api/contable/gastos-proyecto/{tipoModulo}/{gastoId}/documentos
///   GET    /api/contable/gastos-proyecto/{tipoModulo}/{gastoId}/documentos/{docId}/download
///
/// Tipos de modulo validos (normalizados en GastoProyectoService.NormalizeTipoModulo):
///   Terreno, Marketing, OtrosGastos, GastosMunicipales.
///   Cualquier otro valor lanza InvalidOperationException("Tipo de módulo no válido.")
///   -> 500 + UNHANDLED_ERROR (regla del ApiExceptionMiddleware).
///
/// Service retorna entidades POCO, por lo que la serializacion usa camelCase.
///   - GET por id devuelve { gasto, documentos }.
///   - POST/PUT devuelven { idGastoProyecto } (camelCase desde IdGastoProyecto).
///   - DELETE devuelve { ok: true }.
///
/// Validaciones del repo (lanzan InvalidOperationException -> 500+UNHANDLED_ERROR):
///   - IdProyecto <= 0
///   - MontoSoles < 0
///   - IdProveedorTerreno (si viene) debe existir
///   - IdGastoProyecto (si viene) debe existir
///
/// Upload: solo PDF, sin tipoDocumento (siempre "Factura").
///   Guarda en wwwroot/uploads/gastos-proyecto/{modulo}/{gastoId}/facturas/{guid}_{filename}.
/// </summary>
public class GastosProyectoControllerTests : IntegrationTestBase
{
    // IDs del seed
    private const int IdProyecto = 10;                      // Mayta Capac II
    private const int IdProveedorTerreno = 2;               // HOMECENTERS PERUANOS S.A.
    private const string TipoModuloTest = "Terreno";        // modulo valido mas comun

    // ---- Helpers compartidos ----

    /// <summary>
    /// Crea un gasto via POST y devuelve el IdGastoProyecto generado.
    /// </summary>
    private async Task<int> CrearGastoAsync(
        decimal montoSoles = 100m,
        string concepto = "Concepto de prueba",
        string descripcion = "Gasto de prueba")
    {
        var dto = new GastoProyectoUpsertDto
        {
            IdProyecto = IdProyecto,
            IdProveedorTerreno = IdProveedorTerreno,
            Fecha = DateTime.Today,
            Concepto = concepto,
            Moneda = "PEN",
            MontoSoles = montoSoles,
            MontoDolares = 0m,
            TipoCambio = 3.41m,
            Descripcion = descripcion,
            Estado = "Activo",
            Activo = true
        };
        var response = await _client.PostAsJsonAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear gasto. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(body, "idGastoProyecto");
    }

    // ---- Tests: GET (List + Get) ----

    [Test]
    public async Task List_ConTipoModuloValido_RetornaArrayVacio()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}");

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
        var idGasto = await CrearGastoAsync(montoSoles: 5000m, concepto: "COMPRA DE TERRENO LOTE 1");

        // Act
        var response = await _client.GetAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}/{idGasto}");

        // Assert - estructura { gasto, documentos }
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var gasto = JsonHelpers.GetProp(body, "gasto");
        Assert.That(JsonHelpers.GetInt32(gasto, "idGastoProyecto"), Is.EqualTo(idGasto));
        Assert.That(JsonHelpers.GetInt32(gasto, "idProyecto"), Is.EqualTo(IdProyecto));
        Assert.That(JsonHelpers.GetInt32(gasto, "idProveedorTerreno"), Is.EqualTo(IdProveedorTerreno));
        Assert.That(JsonHelpers.GetString(gasto, "tipoModulo"), Is.EqualTo("Terreno"));
        Assert.That(JsonHelpers.GetString(gasto, "concepto"), Is.EqualTo("COMPRA DE TERRENO LOTE 1"));
        Assert.That(JsonHelpers.GetDecimal(gasto, "montoSoles"), Is.EqualTo(5000m));
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
        var response = await _client.GetAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}/999999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ---- Tests: POST / PUT ----

    [Test]
    public async Task Create_ConDatosValidos_RetornaIdYPersiste()
    {
        // Arrange
        var dto = new GastoProyectoUpsertDto
        {
            IdProyecto = IdProyecto,
            IdProveedorTerreno = IdProveedorTerreno,
            Fecha = new DateTime(2026, 7, 1),
            Concepto = "COMPRA DE LOTE 5",
            Moneda = "PEN",
            MontoSoles = 25000m,
            MontoDolares = 0m,
            TipoCambio = 3.41m,
            Descripcion = "Adquisición del Lote 5 del proyecto",
            Estado = "Activo",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}", dto);

        // Assert - 1: HTTP 200 + id devuelto
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var idGasto = JsonHelpers.GetInt32(body, "idGastoProyecto");
        Assert.That(idGasto, Is.GreaterThan(0));

        // Assert - 2: fila persistida en BD con los datos correctos
        var fila = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT TipoModulo, IdProyecto, Concepto, MontoSoles, Activo
              FROM contable.GastoProyecto
              WHERE IdGastoProyecto = @id",
            new { id = idGasto })).FirstOrDefault();
        Assert.That(fila, Is.Not.Null);
        Assert.That((string)fila.TipoModulo, Is.EqualTo("Terreno"));
        Assert.That((int)fila.IdProyecto, Is.EqualTo(IdProyecto));
        Assert.That((string)fila.Concepto, Is.EqualTo("COMPRA DE LOTE 5"));
        Assert.That((decimal)fila.MontoSoles, Is.EqualTo(25000m));
        Assert.That((bool)fila.Activo, Is.True);
    }

    [Test]
    public async Task Update_ConIdExistente_ActualizaDatos()
    {
        // Arrange - crear gasto con MontoSoles=100
        var idGasto = await CrearGastoAsync(montoSoles: 100m, concepto: "ORIGINAL");

        // Act - PUT con MontoSoles=500 y nuevo concepto
        var dto = new GastoProyectoUpsertDto
        {
            IdProyecto = IdProyecto,
            IdProveedorTerreno = IdProveedorTerreno,
            Fecha = DateTime.Today,
            Concepto = "ACTUALIZADO",
            Moneda = "PEN",
            MontoSoles = 500m,
            MontoDolares = 0m,
            TipoCambio = 3.41m,
            Descripcion = null,
            Estado = "Activo",
            Activo = true
        };
        var response = await _client.PutAsJsonAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}/{idGasto}", dto);

        // Assert - 1: HTTP 200 + mismo id
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetInt32(body, "idGastoProyecto"), Is.EqualTo(idGasto));

        // Assert - 2: BD refleja los nuevos valores
        var actualizado = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT MontoSoles, Concepto
              FROM contable.GastoProyecto
              WHERE IdGastoProyecto = @id",
            new { id = idGasto })).FirstOrDefault();
        Assert.That(actualizado, Is.Not.Null);
        Assert.That((decimal)actualizado.MontoSoles, Is.EqualTo(500m),
            "El MontoSoles debe haberse actualizado a 500.");
        Assert.That((string)actualizado.Concepto, Is.EqualTo("ACTUALIZADO"),
            "El Concepto debe haberse actualizado.");
    }

    // ---- Tests: Validacion, Delete y Upload ----

    [Test]
    public async Task Create_ConMontoCeroEnAmbos_RetornaInternalServerError()
    {
        // Arrange - el repo valida que al menos uno de MontoSoles/MontoDolares sea > 0
        // (soles <= 0 && dolares <= 0 -> "Debes ingresar un monto en soles o dólares.")
        var dto = new GastoProyectoUpsertDto
        {
            IdProyecto = IdProyecto,
            IdProveedorTerreno = IdProveedorTerreno,
            Fecha = DateTime.Today,
            Concepto = "MONTO INVALIDO",
            Moneda = "PEN",
            MontoSoles = 0m,    // invalido
            MontoDolares = 0m,  // invalido -> activa la validacion
            TipoCambio = 3.41m,
            Estado = "Activo",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}", dto);

        // Assert - InvalidOperationException -> 500 + UNHANDLED_ERROR
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("UNHANDLED_ERROR"));
        Assert.That(JsonHelpers.GetString(body, "message"), Does.Contain("monto en soles o dólares"));
    }

    [Test]
    public async Task Delete_ConIdExistente_MarcaActivoFalse()
    {
        // Arrange - crear gasto
        var idGasto = await CrearGastoAsync();

        // Act
        var response = await _client.DeleteAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}/{idGasto}");

        // Assert - 1: HTTP 200 con { ok: true }
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetBoolean(body, "ok"), Is.True);

        // Assert - 2: la fila sigue existiendo pero con Activo=0 (soft delete)
        var fila = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT Activo, Estado FROM contable.GastoProyecto WHERE IdGastoProyecto = @id",
            new { id = idGasto })).FirstOrDefault();
        Assert.That(fila, Is.Not.Null);
        Assert.That((bool)fila.Activo, Is.False,
            "DELETE debe ser soft delete (Activo=0), la fila no debe eliminarse fisicamente.");

        // Assert - 3: GET por id sigue devolviendo la fila (no NotFound)
        var getResponse = await _client.GetAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}/{idGasto}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "El GET por id debe seguir funcionando tras soft delete.");
    }

    [Test]
    public async Task UploadDocumentos_ConArchivoPdf_RetornaOkYGuardaFilaEnBD()
    {
        // Arrange - crear gasto
        var idGasto = await CrearGastoAsync();

        // Act - subir un PDF
        using var content = new MultipartFormDataContent();
        var pdfBytes = Encoding.ASCII.GetBytes(
            "%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n1 0 obj\n<<>>\nendobj\nxref\n0 1\n0000000000 65535 f\ntrailer\n<<>>\nstartxref\n0\n%%EOF\n");
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "files", "factura-terreno.pdf");

        var response = await _client.PostAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}/{idGasto}/documentos", content);

        // Assert - 1: HTTP 200 con { ok: true }
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetBoolean(body, "ok"), Is.True);

        // Assert - 2: fila persistida en BD con TipoDocumento=Factura y Extension=.pdf
        var filaDoc = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT TOP 1 TipoDocumento, Extension, NombreArchivo
              FROM contable.GastoProyectoDocumento
              WHERE IdGastoProyecto = @id
              ORDER BY IdGastoProyectoDocumento DESC",
            new { id = idGasto })).FirstOrDefault();
        Assert.That(filaDoc, Is.Not.Null);
        Assert.That((string)filaDoc.TipoDocumento, Is.EqualTo("Factura"));
        Assert.That((string)filaDoc.Extension, Is.EqualTo(".pdf"));
        Assert.That((string)filaDoc.NombreArchivo, Is.EqualTo("factura-terreno.pdf"));

        // Assert - 3: la lista de documentos del GET tiene 1 elemento
        var getDocResponse = await _client.GetAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTest}/{idGasto}/documentos");
        var docs = await getDocResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(docs.GetArrayLength(), Is.EqualTo(1));
        Assert.That(JsonHelpers.GetString(docs[0], "nombreArchivo"), Is.EqualTo("factura-terreno.pdf"));
    }
}
