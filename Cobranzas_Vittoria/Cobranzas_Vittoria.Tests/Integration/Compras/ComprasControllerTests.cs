using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

/// <summary>
/// Pruebas de ComprasController.
///
///   GET    /api/compras/compras?aceptada=&idProveedor=                         -> List
///   GET    /api/compras/compras/pendientes-desde-oc                            -> PendientesDesdeOc
///   GET    /api/compras/compras/{id}                                           -> Get
///   POST   /api/compras/compras                                                -> Crear
///   GET    /api/compras/compras/{id}/documentos                                 -> GetDocumentos
///   POST   /api/compras/compras/{id}/documentos                                 -> UploadDocumentos (multipart)
///   GET    /api/compras/compras/{id}/documentos/{docId}/download                -> DownloadDocumento
///
/// Reglas:
///   - Para crear una Compra se necesita una OC existente (no se valida estado).
///   - NumeroCompra es requerido. Si el enviado ya existe, el repo autogenera
///     (MAX(TRY_CAST(NumeroCompra AS INT)) + 1, mismo patrón que OC).
///   - Cálculo de IGV:
///       Si IncluyeIGV=true:  SubtotalSinIGV = MontoTotal / 1.18
///                            MontoIGV       = MontoTotal - SubtotalSinIGV
///       Si IncluyeIGV=false: SubtotalSinIGV = MontoTotal, MontoIGV = 0.
///   - Get devuelve { compra, items, documentos }.
///   - El upload guarda el archivo físico en wwwroot/uploads/compras/{id}/{guid}_{filename}
///     y persiste la fila en compras.CompraDocumento.
///
/// Importante: el repo retorna DapperRows (IDictionary&lt;string, object&gt;), por lo que
/// System.Text.Json NO aplica camelCase a las claves. Los asserts usan los nombres
/// del SQL (PascalCase) via JsonHelpers (case-insensitive).
///   - ListAsync NO proyecta c.Aceptada en el SELECT (sí la usa en el WHERE del filtro).
///   - GetAsync NO proyecta c.IdProveedor (proyecta 'Proveedor' = RazonSocial del JOIN).
/// </summary>
public class ComprasControllerTests : IntegrationTestBase
{
    // IdProveedor=2 = ACG EDIFICACIONES (Activo=1)
    private const int IdProveedor = 2;

    // Materiales del seed
    private const int IdMaterialAlbanileria = 2;
    private const int IdMaterialCasco = 6;

    // ---- Helpers compartidos ----

    private async Task<int> CrearOrdenAsync(int idRequerimiento)
    {
        var dto = new OrdenCompraCreateDto
        {
            NumeroOrdenCompra = string.Empty,  // el server lo autogenera
            IdRequerimiento = idRequerimiento,
            IdProveedor = IdProveedor,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            FechaOrdenCompra = DateTime.Today,
            Descripcion = "OC de prueba para Compra",
            IdUsuarioCreacion = SeedIds.IngenieroId,
            Items = new List<OrdenCompraDetalleCreateDto>
            {
                new() { IdMaterial = IdMaterialAlbanileria, Cantidad = 10m, IdProveedor = IdProveedor, PrecioUnitario = 15.50m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/compras/ordenes-compra", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear OC. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("idOrdenCompra").GetInt32();
    }

    private async Task<int> CrearCompraAsync(int idOc, bool incluyeIgv = false, List<CompraDetalleCreateDto>? items = null)
    {
        var dto = new CompraCreateDto
        {
            NumeroCompra = string.Empty,  // el server lo autogenera
            IdOrdenCompra = idOc,
            IdProveedor = IdProveedor,
            FechaCompra = DateTime.Today,
            IncluyeIGV = incluyeIgv,
            Observacion = "Compra de prueba de integración",
            Items = items ?? new List<CompraDetalleCreateDto>
            {
                new() { IdMaterial = IdMaterialAlbanileria, Cantidad = 10m, PrecioUnitario = 15.50m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/compras/compras", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear Compra. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("idCompra").GetInt32();
    }

    private async Task<int> SubirDocumentoAsync(int idCompra, string filename = "factura.pdf")
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes($"Contenido fake del PDF {Guid.NewGuid():N}");
        var fileContent = new StreamContent(new MemoryStream(fileBytes));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        // El nombre "files" debe coincidir con el parámetro del controller ([FromForm] List<IFormFile> files)
        content.Add(fileContent, "files", filename);

        var response = await _client.PostAsync(
            $"/api/compras/compras/{idCompra}/documentos",
            content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al subir documento. Body: {await response.Content.ReadAsStringAsync()}");
        return idCompra;
    }

    // ---- Tests ----

    [Test]
    public async Task List_SinFiltros_RetornaArrayVacio()
    {
        // Act - la tabla compras.Compra se limpia antes de cada test
        var response = await _client.GetAsync("/api/compras/compras");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task List_FiltradoPorIdProveedor_RetornaComprasCoincidentes()
    {
        // Arrange - crear 2 compras del mismo proveedor (cada una desde su propia OC)
        var idReq1 = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idReq2 = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc1 = await CrearOrdenAsync(idReq1);
        var idOc2 = await CrearOrdenAsync(idReq2);
        await CrearCompraAsync(idOc1);
        await CrearCompraAsync(idOc2);

        // Act
        var response = await _client.GetAsync(
            $"/api/compras/compras?idProveedor={IdProveedor}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task List_FiltradoPorAceptadaFalse_RetornaComprasNoAceptadas()
    {
        // Arrange - crear 1 compra (todas las nuevas tienen Aceptada=0)
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc = await CrearOrdenAsync(idReq);
        await CrearCompraAsync(idOc);

        // Act
        var response = await _client.GetAsync("/api/compras/compras?aceptada=false");

        // Assert
        // El repo filtra por c.Aceptada en el WHERE pero NO la proyecta en el SELECT,
        // asi que la respuesta no trae la propiedad 'Aceptada'. Verificamos el efecto del filtro
        // indirectamente: si filtra bien, solo aparece la compra creada (Aceptada=0 al insertar).
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));
        // El IdCompra de la fila retornada debe corresponder a la OC creada
        Assert.That(JsonHelpers.GetInt32(body[0], "IdCompra"), Is.GreaterThan(0));
    }

    [Test]
    public async Task PendientesDesdeOc_RetornaOcsSinCompra()
    {
        // Arrange - crear 2 OCs: una con Compra, otra sin Compra
        var idReq1 = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idReq2 = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc1 = await CrearOrdenAsync(idReq1);
        var idOc2 = await CrearOrdenAsync(idReq2);
        await CrearCompraAsync(idOc1);   // OC 1 ya tiene compra
        // OC 2 queda pendiente

        // Act
        var response = await _client.GetAsync("/api/compras/compras/pendientes-desde-oc");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1),
            "Solo la OC sin Compra debe aparecer en pendientes.");
        Assert.That(JsonHelpers.GetInt32(body[0], "IdOrdenCompra"), Is.EqualTo(idOc2));
    }

    [Test]
    public async Task Get_ConIdExistente_RetornaCompraConItemsYDocumentos()
    {
        // Arrange
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc = await CrearOrdenAsync(idReq);
        var idCompra = await CrearCompraAsync(idOc);

        // Act
        var response = await _client.GetAsync($"/api/compras/compras/{idCompra}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Estructura: { compra, items, documentos }
        var compra = JsonHelpers.GetProp(body, "compra");
        Assert.That(JsonHelpers.GetInt32(compra, "IdCompra"), Is.EqualTo(idCompra));
        Assert.That(JsonHelpers.GetInt32(compra, "IdOrdenCompra"), Is.EqualTo(idOc));
        // El repo no proyecta c.IdProveedor; proyecta 'Proveedor' (RazonSocial del JOIN).
        Assert.That(JsonHelpers.GetString(compra, "Proveedor"), Is.EqualTo("ACG EDIFICACIONES EIRL"));
        Assert.That(JsonHelpers.GetString(compra, "NumeroCompra"), Is.Not.Empty);
        // MontoTotal = 10 * 15.50 = 155.00 (sin IGV)
        Assert.That(JsonHelpers.GetDecimal(compra, "MontoTotal"), Is.EqualTo(155.00m));
        Assert.That(JsonHelpers.GetDecimal(compra, "MontoIGV"), Is.EqualTo(0m),
            "Sin IGV, MontoIGV debe ser 0.");
        Assert.That(JsonHelpers.GetDecimal(compra, "SubtotalSinIGV"), Is.EqualTo(155.00m));

        var items = JsonHelpers.GetProp(body, "items");
        Assert.That(items.GetArrayLength(), Is.EqualTo(1));
        Assert.That(JsonHelpers.GetInt32(items[0], "IdMaterial"), Is.EqualTo(IdMaterialAlbanileria));
        Assert.That(JsonHelpers.GetDecimal(items[0], "Subtotal"), Is.EqualTo(155.00m));

        // Sin documentos subidos
        var documentos = JsonHelpers.GetProp(body, "documentos");
        Assert.That(documentos.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(documentos.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task Get_ConIdInexistente_RetornaNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/compras/compras/99999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Crear_ConOrdenCompraYProveedorValidos_RetornaOkEPersisteCompraYDetalle()
    {
        // Arrange
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc = await CrearOrdenAsync(idReq);

        // Act - 2 items con IGV
        var idCompra = await CrearCompraAsync(idOc, incluyeIgv: true, items: new List<CompraDetalleCreateDto>
        {
            new() { IdMaterial = IdMaterialAlbanileria, Cantidad = 5m, PrecioUnitario = 20m },
            new() { IdMaterial = IdMaterialCasco, Cantidad = 3m, PrecioUnitario = 100m }
        });

        // Assert - 1: BD - cabecera
        var cabecera = (await DbHelpers.QueryAsync<CompraRow>(
            "SELECT IdCompra AS Id, NumeroCompra AS Numero, Aceptada AS Aceptada, " +
            "  SubtotalSinIGV AS Subtotal, MontoIGV AS Igv, MontoTotal AS Total, IdProveedor AS Prov " +
            "FROM compras.Compra WHERE IdCompra = @id",
            new { id = idCompra })).Single();
        Assert.That(cabecera.Numero, Is.Not.Empty);
        Assert.That(cabecera.Aceptada, Is.False);
        Assert.That(cabecera.Prov, Is.EqualTo(IdProveedor));

        // Total = (5*20) + (3*100) = 100 + 300 = 400
        // SubtotalSinIGV = 400 / 1.18 = 338.98 (redondeado)
        // MontoIGV = 400 - 338.98 = 61.02
        Assert.That(cabecera.Total, Is.EqualTo(400.00m));
        Assert.That(cabecera.Igv, Is.EqualTo(61.02m));
        Assert.That(cabecera.Subtotal, Is.EqualTo(338.98m));

        // Assert - 2: BD - detalle
        var totalDetalles = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM compras.CompraDetalle WHERE IdCompra = @id",
            new { id = idCompra });
        Assert.That(totalDetalles, Is.EqualTo(2));
    }

    [Test]
    public async Task UploadDocumentos_ConArchivo_RetornaOkYGuardaFilaEnBD()
    {
        // Arrange
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc = await CrearOrdenAsync(idReq);
        var idCompra = await CrearCompraAsync(idOc);

        // Act
        await SubirDocumentoAsync(idCompra, "factura-001.pdf");

        // Assert: 1 fila en CompraDocumento
        var total = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM compras.CompraDocumento WHERE IdCompra = @id",
            new { id = idCompra });
        Assert.That(total, Is.EqualTo(1));

        // Verificar campos clave
        var doc = (await DbHelpers.QueryAsync<DocRow>(
            "SELECT NombreArchivo AS Nombre, Extension AS Ext, TipoDocumento AS Tipo " +
            "FROM compras.CompraDocumento WHERE IdCompra = @id",
            new { id = idCompra })).Single();
        Assert.That(doc.Nombre, Does.EndWith("factura-001.pdf"));
        Assert.That(doc.Ext, Is.EqualTo(".pdf"));
        Assert.That(doc.Tipo, Is.EqualTo("Factura"));
    }

    [Test]
    public async Task GetDocumentos_ConCompraExistente_RetornaListaDeDocumentos()
    {
        // Arrange
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc = await CrearOrdenAsync(idReq);
        var idCompra = await CrearCompraAsync(idOc);
        await SubirDocumentoAsync(idCompra, "factura-A.pdf");

        // Act
        var response = await _client.GetAsync($"/api/compras/compras/{idCompra}/documentos");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));
        Assert.That(JsonHelpers.GetInt32(body[0], "IdCompra"), Is.EqualTo(idCompra));
        Assert.That(JsonHelpers.GetString(body[0], "NombreArchivo"), Does.EndWith("factura-A.pdf"));
    }

    [Test]
    public async Task DownloadDocumento_ConIdExistente_RetornaArchivoFisico()
    {
        // Arrange
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc = await CrearOrdenAsync(idReq);
        var idCompra = await CrearCompraAsync(idOc);
        await SubirDocumentoAsync(idCompra, "factura-DL.pdf");

        // Obtener el IdCompraDocumento recién creado
        var idDoc = await DbHelpers.QueryScalarAsync<int>(
            "SELECT IdCompraDocumento FROM compras.CompraDocumento WHERE IdCompra = @id",
            new { id = idCompra });

        // Act
        var response = await _client.GetAsync(
            $"/api/compras/compras/{idCompra}/documentos/{idDoc}/download");

        // Assert: 200 OK + bytes no vacíos
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.That(bytes, Is.Not.Empty,
            "El download debe devolver el contenido del archivo subido.");
    }

    // --- Tipos de proyección para Dapper ---
    private record CompraRow(int Id, string Numero, bool Aceptada,
        decimal Subtotal, decimal Igv, decimal Total, int Prov);
    private record DocRow(string Nombre, string? Ext, string Tipo);
}
