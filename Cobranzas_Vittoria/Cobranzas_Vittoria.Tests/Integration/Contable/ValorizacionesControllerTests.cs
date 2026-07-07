using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Valorizaciones;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

/// <summary>
/// Pruebas de ValorizacionesController (11 endpoints, el mas grande del proyecto).
///
///   GET    /api/contable/valorizaciones/configuraciones
///   POST   /api/contable/valorizaciones/configuraciones
///   POST   /api/contable/valorizaciones/reglas-proveedor
///   GET    /api/contable/valorizaciones
///   GET    /api/contable/valorizaciones/configuracion/{idConfiguracion}
///   GET    /api/contable/valorizaciones/{id}
///   POST   /api/contable/valorizaciones
///   POST   /api/contable/valorizaciones/detalle
///   DELETE /api/contable/valorizaciones/detalle/{id}
///   POST   /api/contable/valorizaciones/detalle/{idDetalle}/archivos
///   GET    /api/contable/valorizaciones/detalle/{idDetalle}/archivos/{idArchivo}/download
///
/// El repo retorna `object` con propiedades camelCase (anónimos C# proyectados),
/// por lo que la serializacion aplica la politica camelCase (no son DapperRows
/// directos). El Service es un wrapper simple. JsonHelpers (case-insensitive)
/// se mantiene por consistencia.
///
/// Estructura tipica de respuesta:
///   - List: array de objetos planos.
///   - GetById/GetByConfiguracion: { cabecera, detalle, resumen }.
///   - Upsert: { idConfiguracion | idValorizacion | idDetalle, ... }.
///   - DeleteDetalle: { ok: true }.
///
/// Validaciones:
///   - UploadArchivos: files.Count==0 -> 400 "Debes adjuntar al menos un archivo."
///                       extension != .pdf -> 400 "Solo se permiten archivos PDF."
///                       tipoDocumento vacio -> 400 "Debes indicar el tipo de documento."
///   - UpsertReglaProveedor: BUG del repo. El SP maestra.usp_ProveedorReglaValorizacion_Upsert
///                       solo tiene 3 params (@IdProveedor, @PorcentajeGarantia, @PorcentajeDetraccion)
///                       pero el repo le pasa 4 (incluye dto.Usuario). Resultado: 500+SQL_ERROR
///                       "too many arguments specified". Test documenta el bug.
///
/// Cadena de setup: Configuracion -> Valorizacion -> Detalle (cada uno requiere el anterior).
/// </summary>
public class ValorizacionesControllerTests : IntegrationTestBase
{
    // IDs del seed
    private const int IdProyecto = 10;                // Mayta Capac II
    private const int IdProveedor = 2;                // ACG EDIFICACIONES EIRL
    private const int IdEspecialidad = 2;            // Albañilería

    // ---- Helpers compartidos (cadena de setup) ----

    private async Task<int> CrearConfiguracionAsync(decimal montoCotizacion = 50000m)
    {
        var dto = new ProveedorEspecialidadCotizacionUpsertDto
        {
            IdProyecto = IdProyecto,
            IdProveedor = IdProveedor,
            IdEspecialidad = IdEspecialidad,
            Moneda = "PEN",
            MontoCotizacion = montoCotizacion,
            Usuario = "test"
        };
        var response = await _client.PostAsJsonAsync(
            "/api/contable/valorizaciones/configuraciones", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear configuracion. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(body, "idConfiguracion");
    }

    private async Task<int> CrearValorizacionAsync(int idConfiguracion, string periodo = "VAL-2026-07")
    {
        var dto = new ValorizacionUpsertDto
        {
            IdConfiguracion = idConfiguracion,
            Periodo = periodo,
            Observacion = "Valorizacion de prueba",
            Usuario = "test"
        };
        var response = await _client.PostAsJsonAsync(
            "/api/contable/valorizaciones", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear valorizacion. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(body, "idValorizacion");
    }

    private async Task<int> CrearDetalleAsync(int idValorizacion, int idConfiguracion, decimal montoFactura = 10000m)
    {
        var dto = new ValorizacionDetalleUpsertDto
        {
            IdValorizacion = idValorizacion,
            IdConfiguracion = idConfiguracion,
            FechaFactura = DateTime.Today,
            NumeroFactura = "F001-00001",
            MontoFactura = montoFactura,
            Descripcion = "Factura de prueba",
            OtrosDescuentos = 0m,
            NumeroOperacion = string.Empty,
            BancoTransferencia = string.Empty,
            BancoDestino = string.Empty,
            MontoTransferido = 0m,
            PorcentajeDetraccionAplicado = 0.04m,
            PorcentajeGarantiaAplicado = 0.05m,
            Usuario = "test"
        };
        var response = await _client.PostAsJsonAsync(
            "/api/contable/valorizaciones/detalle", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear detalle. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(body, "idDetalle");
    }

    // ---- Tests: Configuraciones ----

    [Test]
    public async Task ListConfiguraciones_FiltroInexistente_RetornaArrayVacio()
    {
        // Act - filtrar por un IdProyecto que no existe (la tabla maestra.ProveedorEspecialidadCotizacion
        // esta en TablesToIgnore del Respawn, asi que los datos seed y de tests previos persisten).
        var response = await _client.GetAsync(
            "/api/contable/valorizaciones/configuraciones?idProyecto=999999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task UpsertConfiguracion_ConDatosValidos_RetornaIdYPersiste()
    {
        // Arrange
        var dto = new ProveedorEspecialidadCotizacionUpsertDto
        {
            IdProyecto = IdProyecto,
            IdProveedor = IdProveedor,
            IdEspecialidad = IdEspecialidad,
            Moneda = "PEN",
            MontoCotizacion = 75000m,
            Usuario = "test"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/contable/valorizaciones/configuraciones", dto);

        // Assert - 1: HTTP 200 + id devuelto
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var idConfig = JsonHelpers.GetInt32(body, "idConfiguracion");
        Assert.That(idConfig, Is.GreaterThan(0));

        // Assert - 2: fila persistida en BD
        var fila = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT IdProyecto, IdProveedor, IdEspecialidad, Moneda, MontoCotizacion, Activo
              FROM maestra.ProveedorEspecialidadCotizacion
              WHERE IdProveedorEspecialidadCotizacion = @id",
            new { id = idConfig })).FirstOrDefault();
        Assert.That(fila, Is.Not.Null);
        Assert.That((int)fila.IdProyecto, Is.EqualTo(IdProyecto));
        Assert.That((int)fila.IdProveedor, Is.EqualTo(IdProveedor));
        Assert.That((int)fila.IdEspecialidad, Is.EqualTo(IdEspecialidad));
        Assert.That((string)fila.Moneda, Is.EqualTo("PEN"));
        Assert.That((decimal)fila.MontoCotizacion, Is.EqualTo(75000m));
    }

    [Test]
    public async Task UpsertReglaProveedor_ConDatosValidos_RetornaInternalServerErrorPorBugDelRepo()
    {
        // Arrange
        // BUG documentado: el SP maestra.usp_ProveedorReglaValorizacion_Upsert solo tiene 3
        // parametros (@IdProveedor, @PorcentajeGarantia, @PorcentajeDetraccion) pero el repo le
        // pasa 4 incluyendo dto.Usuario. Esto siempre produce 500+SQL_ERROR.
        // Cuando se arregle el repo, este test debera actualizarse a esperar 200.
        var dto = new ProveedorReglaValorizacionUpsertDto
        {
            IdProveedor = IdProveedor,
            PorcentajeGarantia = 0.10m,
            PorcentajeDetraccion = 0.08m,
            Usuario = "test"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/contable/valorizaciones/reglas-proveedor", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("SQL_ERROR"));
    }

    // ---- Tests: Valorizaciones ----

    [Test]
    public async Task List_SinFiltros_RetornaArrayVacio()
    {
        // Act
        var response = await _client.GetAsync("/api/contable/valorizaciones");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetById_ConIdInexistente_RetornaCabeceraNullYArraysVacios()
    {
        // Act
        var response = await _client.GetAsync("/api/contable/valorizaciones/999999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // El repo retorna { cabecera: null, detalle: [], resumen: null } cuando no existe
        Assert.That(JsonHelpers.HasProp(body, "cabecera"), Is.True);
        Assert.That(JsonHelpers.GetProp(body, "cabecera").ValueKind, Is.EqualTo(JsonValueKind.Null));
        var detalle = JsonHelpers.GetProp(body, "detalle");
        Assert.That(detalle.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(detalle.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task UpsertValorizacion_ConConfiguracionExistente_RetornaIdYNumeroValorizacion()
    {
        // Arrange - primero crear la configuracion (la valorizacion depende de esta)
        var idConfig = await CrearConfiguracionAsync(montoCotizacion: 60000m);

        var dto = new ValorizacionUpsertDto
        {
            IdConfiguracion = idConfig,
            Periodo = "VAL-2026-08",
            Observacion = "Primera valorizacion del periodo",
            Usuario = "test"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/contable/valorizaciones", dto);

        // Assert - 1: HTTP 200 + id + numero
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var idValorizacion = JsonHelpers.GetInt32(body, "idValorizacion");
        Assert.That(idValorizacion, Is.GreaterThan(0));
        Assert.That(JsonHelpers.GetString(body, "numeroValorizacion"), Is.Not.Empty);

        // Assert - 2: fila persistida en BD
        var fila = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT IdProveedorEspecialidadCotizacion, NumeroValorizacion, Observacion
              FROM contable.Valorizacion
              WHERE IdValorizacion = @id",
            new { id = idValorizacion })).FirstOrDefault();
        Assert.That(fila, Is.Not.Null);
        Assert.That((int)fila.IdProveedorEspecialidadCotizacion, Is.EqualTo(idConfig));
        Assert.That((string)fila.NumeroValorizacion, Is.EqualTo("VAL-2026-08"));
        Assert.That((string)fila.Observacion, Is.EqualTo("Primera valorizacion del periodo"));
    }

    // ---- Tests: Detalle ----

    [Test]
    public async Task UpsertDetalle_ConValorizacionExistente_RetornaIdDetalleYPersiste()
    {
        // Arrange - cadena: Configuracion -> Valorizacion -> Detalle
        var idConfig = await CrearConfiguracionAsync();
        var idValorizacion = await CrearValorizacionAsync(idConfig);

        var dto = new ValorizacionDetalleUpsertDto
        {
            IdValorizacion = idValorizacion,
            IdConfiguracion = idConfig,
            FechaFactura = new DateTime(2026, 7, 15),
            NumeroFactura = "F002-99999",
            MontoFactura = 12500.50m,
            Descripcion = "Factura de servicios",
            OtrosDescuentos = 100m,
            NumeroOperacion = "OP-001",
            BancoTransferencia = "BCP",
            BancoDestino = "BBVA",
            MontoTransferido = 0m,
            PorcentajeDetraccionAplicado = 0.04m,
            PorcentajeGarantiaAplicado = 0.05m,
            Usuario = "test"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/contable/valorizaciones/detalle", dto);

        // Assert - 1: HTTP 200 + idDetalle devuelto
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var idDetalle = JsonHelpers.GetInt32(body, "idDetalle");
        Assert.That(idDetalle, Is.GreaterThan(0));

        // Assert - 2: fila persistida en BD
        var fila = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT IdValorizacion, NumeroFactura, MontoFactura, Descripcion
              FROM contable.ValorizacionDetalle
              WHERE IdValorizacionDetalle = @id",
            new { id = idDetalle })).FirstOrDefault();
        Assert.That(fila, Is.Not.Null);
        Assert.That((int)fila.IdValorizacion, Is.EqualTo(idValorizacion));
        Assert.That((string)fila.NumeroFactura, Is.EqualTo("F002-99999"));
        Assert.That((decimal)fila.MontoFactura, Is.EqualTo(12500.50m));
        Assert.That((string)fila.Descripcion, Is.EqualTo("Factura de servicios"));
    }

    [Test]
    public async Task DeleteDetalle_ConIdExistente_RetornaOkYEliminaFila()
    {
        // Arrange - cadena completa
        var idConfig = await CrearConfiguracionAsync();
        var idValorizacion = await CrearValorizacionAsync(idConfig);
        var idDetalle = await CrearDetalleAsync(idValorizacion, idConfig);

        // Act
        var response = await _client.DeleteAsync(
            $"/api/contable/valorizaciones/detalle/{idDetalle}");

        // Assert - 1: HTTP 200 con { ok: true }
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetBoolean(body, "ok"), Is.True);

        // Assert - 2: la fila fue eliminada fisicamente (no soft delete)
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM contable.ValorizacionDetalle WHERE IdValorizacionDetalle = @id",
            new { id = idDetalle });
        Assert.That(count, Is.EqualTo(0),
            "DeleteDetalle debe eliminar la fila fisicamente (DELETE directo, no soft).");
    }

    [Test]
    public async Task UploadArchivos_ConArchivoPdf_RetornaOkYGuardaFilaEnBD()
    {
        // Arrange - cadena completa
        var idConfig = await CrearConfiguracionAsync();
        var idValorizacion = await CrearValorizacionAsync(idConfig);
        var idDetalle = await CrearDetalleAsync(idValorizacion, idConfig);

        // Act - subir un PDF como Factura
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Factura"), "tipoDocumento");
        var pdfBytes = Encoding.ASCII.GetBytes(
            "%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n1 0 obj\n<<>>\nendobj\nxref\n0 1\n0000000000 65535 f\ntrailer\n<<>>\nstartxref\n0\n%%EOF\n");
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "files", "factura-valorizacion.pdf");

        var response = await _client.PostAsync(
            $"/api/contable/valorizaciones/detalle/{idDetalle}/archivos", content);

        // Assert - 1: HTTP 200 con { ok: true }
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetBoolean(body, "ok"), Is.True);

        // Assert - 2: fila persistida en BD con Extension=.pdf
        var fila = (await DbHelpers.QueryAsync<dynamic>(
            @"SELECT TOP 1 NombreArchivo, Extension
              FROM contable.ValorizacionDetalleArchivo
              WHERE IdValorizacionDetalle = @id
              ORDER BY IdValorizacionDetalleArchivo DESC",
            new { id = idDetalle })).FirstOrDefault();
        Assert.That(fila, Is.Not.Null);
        Assert.That((string)fila.NombreArchivo, Is.EqualTo("factura-valorizacion.pdf"));
        Assert.That((string)fila.Extension, Is.EqualTo(".pdf"));
    }
}
