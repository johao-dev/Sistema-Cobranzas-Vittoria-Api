using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Almacen;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Almacen;

/// <summary>
/// Pruebas de KardexController.
///
///   GET   /api/almacen/kardex/movimientos?idCompra=&idMaterial=&idEspecialidad=&fechaDesde=&fechaHasta=
///   POST  /api/almacen/kardex/salidas-manuales (legacy)
///
/// Reglas de negocio:
///   - Las ENTRADAS del Kardex se derivan automaticamente de CompraDetalle
///     (cada CompraDetalle aporta Entrada = Cantidad).
///   - Las SALIDAS se registran manualmente via POST /salidas y se guardan
///     en almacen.KardexMovimiento con TipoMovimiento='SALIDA'.
///   - Stock = SUM(Entrada) - SUM(Salida), agrupado por (IdCompra, IdMaterial).
///
/// Validaciones del SP almacen.usp_Kardex_RegistrarSalida:
///   - 51001: CantidadSalida &lt;= 0
///   - 51004: Material no pertenece al CompraDetalle de la compra indicada
///   - 51003: Salida excede el stock disponible para esa compra
///   - Si IdEspecialidad viene null, el SP lo autocompleta desde maestra.Material.
///
/// Manejo de errores: ApiExceptionMiddleware captura SqlException y responde
/// 500 con { ok:false, error:"SQL_ERROR", message:&lt;mensaje del SP&gt; }.
///
/// El repo retorna DapperRows (PascalCase), por lo que los asserts usan JsonHelpers.
/// </summary>
public class KardexControllerTests : IntegrationTestBase
{
    private const int IdProveedor = 2;            // ACG EDIFICACIONES EIRL (Activo=1)
    private const int IdMaterialAlbanileria = 2;  // Materiales seed: 2 = Albañilería
    private const int IdMaterialCasco = 6;        // Materiales seed: 6 = Casco

    // ---- Helpers compartidos ----

    /// <summary>
    /// Crea el flujo completo Requerimiento -> OC -> Compra con la cantidad indicada
    /// del material. La Compra queda con stock inicial = cantidad.
    /// </summary>
    private async Task<int> CrearCompraConStockAsync(decimal cantidad = 10m, int idMaterial = IdMaterialAlbanileria)
    {
        var idReq = await RequerimientoBuilder.Nuevo()
            .ConItem(idMaterial, cantidad)  // agrega item con el material deseado
            .CrearEnviadoOcAsync(_client);

        var idOc = await CrearOrdenAsync(idReq, idMaterial, cantidad);
        var idCompra = await CrearCompraAsync(idOc, idMaterial, cantidad);
        return idCompra;
    }

    private async Task<int> CrearOrdenAsync(int idRequerimiento, int idMaterial, decimal cantidad)
    {
        var dto = new OrdenCompraCreateDto
        {
            NumeroOrdenCompra = string.Empty,
            IdRequerimiento = idRequerimiento,
            IdProveedor = IdProveedor,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            FechaOrdenCompra = DateTime.Today,
            Descripcion = "OC para test de Kardex",
            IdUsuarioCreacion = SeedIds.IngenieroId,
            Items = new List<OrdenCompraDetalleCreateDto>
            {
                new() { IdMaterial = idMaterial, Cantidad = cantidad, IdProveedor = IdProveedor, PrecioUnitario = 10m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/compras/ordenes-compra", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear OC. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(body, "idOrdenCompra");
    }

    private async Task<int> CrearCompraAsync(int idOc, int idMaterial, decimal cantidad)
    {
        var dto = new CompraCreateDto
        {
            NumeroCompra = string.Empty,
            IdOrdenCompra = idOc,
            IdProveedor = IdProveedor,
            FechaCompra = DateTime.Today,
            IncluyeIGV = false,
            Observacion = "Compra para test de Kardex",
            Items = new List<CompraDetalleCreateDto>
            {
                new() { IdMaterial = idMaterial, Cantidad = cantidad, PrecioUnitario = 10m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/compras/compras", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear Compra. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(body, "idCompra");
    }

    // ---- Tests: GET /movimientos ----

    [Test]
    public async Task List_SinFiltros_RetornaArrayVacio()
    {
        // Act
        var response = await _client.GetAsync("/api/almacen/kardex/movimientos");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task List_ConUnaCompra_RetornaMovimientoDeEntrada()
    {
        // Arrange - Compra con stock 10 del material de Albañilería
        var idCompra = await CrearCompraConStockAsync(cantidad: 10m);

        // Act
        var response = await _client.GetAsync("/api/almacen/kardex/movimientos");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1),
            "Una Compra con un item debe generar exactamente una fila agrupada de Kardex.");

        var fila = body[0];
        Assert.That(JsonHelpers.GetInt32(fila, "IdCompra"), Is.EqualTo(idCompra));
        Assert.That(JsonHelpers.GetInt32(fila, "IdMaterial"), Is.EqualTo(IdMaterialAlbanileria));
        Assert.That(JsonHelpers.GetDecimal(fila, "Entrada"), Is.EqualTo(10m));
        Assert.That(JsonHelpers.GetDecimal(fila, "Salida"), Is.EqualTo(0m));
        Assert.That(JsonHelpers.GetDecimal(fila, "Stock"), Is.EqualTo(10m));
    }

    [Test]
    public async Task List_FiltradoPorIdMaterial_RetornaSoloCoincidentes()
    {
        // Arrange - 2 Compras con materiales distintos
        var idCompra1 = await CrearCompraConStockAsync(cantidad: 5m, idMaterial: IdMaterialAlbanileria);
        var idCompra2 = await CrearCompraConStockAsync(cantidad: 8m, idMaterial: IdMaterialCasco);

        // Act - filtrar por material de Casco
        var response = await _client.GetAsync(
            $"/api/almacen/kardex/movimientos?idMaterial={IdMaterialCasco}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1),
            "Solo la Compra del material filtrado debe aparecer.");
        Assert.That(JsonHelpers.GetInt32(body[0], "IdCompra"), Is.EqualTo(idCompra2));
        Assert.That(JsonHelpers.GetInt32(body[0], "IdMaterial"), Is.EqualTo(IdMaterialCasco));
        Assert.That(JsonHelpers.GetDecimal(body[0], "Entrada"), Is.EqualTo(8m));
        Assert.That(JsonHelpers.GetDecimal(body[0], "Stock"), Is.EqualTo(8m));
    }

    [Test]
    public async Task List_ConSalidaPrevia_MuestraStockDecrementado()
    {
        // Arrange - Compra con stock 10 + salida manual de 3
        var idCompra = await CrearCompraConStockAsync(cantidad: 10m);
        await RegistrarSalidaAsync(idCompra, IdMaterialAlbanileria, 3m);

        // Act
        var response = await _client.GetAsync(
            $"/api/almacen/kardex/movimientos?idCompra={idCompra}");

        // Assert - agrupado por (IdCompra, IdMaterial) muestra Entrada=10, Salida=3, Stock=7
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));
        var fila = body[0];
        Assert.That(JsonHelpers.GetDecimal(fila, "Entrada"), Is.EqualTo(10m));
        Assert.That(JsonHelpers.GetDecimal(fila, "Salida"), Is.EqualTo(3m));
        Assert.That(JsonHelpers.GetDecimal(fila, "Stock"), Is.EqualTo(7m));
    }

    // ---- Tests: POST /salidas ----

    [Test]
    public async Task RegistrarSalida_ConStockSuficiente_RetornaOkYDecrementaStock()
    {
        // Arrange - Compra con stock 10
        var idCompra = await CrearCompraConStockAsync(cantidad: 10m);

        // Act - salida de 5
        var dto = new KardexSalidaCreateDto
        {
            IdCompra = idCompra,
            IdMaterial = IdMaterialAlbanileria,
            IdEspecialidad = SeedIds.EspecialidadAlbanileria,
            FechaMovimiento = DateTime.Today,
            CantidadSalida = 5m,
            Observacion = "Salida de prueba OK"
        };
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas-manuales", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetBoolean(body, "ok"), Is.True);
        Assert.That(JsonHelpers.GetDecimal(body, "stockActual"), Is.EqualTo(5m));

        // Verificar persistencia: 1 fila en KardexMovimiento con TipoMovimiento='SALIDA'
        var total = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM almacen.KardexMovimiento " +
            "WHERE IdCompra = @id AND TipoMovimiento = 'SALIDA'",
            new { id = idCompra });
        Assert.That(total, Is.EqualTo(1));
    }

    [Test]
    public async Task RegistrarSalida_ConCantidadCero_Retorna500()
    {
        // Arrange - Compra con stock 10
        var idCompra = await CrearCompraConStockAsync(cantidad: 10m);

        // Act - salida con cantidad 0 (SP 51001)
        var dto = new KardexSalidaCreateDto
        {
            IdCompra = idCompra,
            IdMaterial = IdMaterialAlbanileria,
            IdEspecialidad = SeedIds.EspecialidadAlbanileria,
            FechaMovimiento = DateTime.Today,
            CantidadSalida = 0m
        };
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas-manuales", dto);

        // Assert - el middleware traduce SqlException 51001 a 500 con error=SQL_ERROR
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("SQL_ERROR"));
        Assert.That(JsonHelpers.GetString(body, "message"), Does.Contain("mayor a cero"));
    }

    [Test]
    public async Task RegistrarSalida_QueExcedeStock_Retorna500()
    {
        // Arrange - Compra con stock 10
        var idCompra = await CrearCompraConStockAsync(cantidad: 10m);

        // Act - salida de 15 (excede el stock disponible, SP 51003)
        var dto = new KardexSalidaCreateDto
        {
            IdCompra = idCompra,
            IdMaterial = IdMaterialAlbanileria,
            IdEspecialidad = SeedIds.EspecialidadAlbanileria,
            FechaMovimiento = DateTime.Today,
            CantidadSalida = 15m
        };
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas-manuales", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("SQL_ERROR"));
        Assert.That(JsonHelpers.GetString(body, "message"), Does.Contain("stock disponible"));
    }

    [Test]
    public async Task RegistrarSalida_ConMaterialNoEnCompra_Retorna500()
    {
        // Arrange - Compra del material de Albañilería
        var idCompra = await CrearCompraConStockAsync(cantidad: 10m, idMaterial: IdMaterialAlbanileria);

        // Act - intentar salida del material de Casco (no pertenece al CompraDetalle de esta compra, SP 51004)
        var dto = new KardexSalidaCreateDto
        {
            IdCompra = idCompra,
            IdMaterial = IdMaterialCasco,
            IdEspecialidad = SeedIds.EspecialidadCasco,
            FechaMovimiento = DateTime.Today,
            CantidadSalida = 1m
        };
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas-manuales", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("SQL_ERROR"));
        Assert.That(JsonHelpers.GetString(body, "message"), Does.Contain("no pertenece a la compra"));
    }

    // ---- Helper privado extra ----

    private async Task RegistrarSalidaAsync(int idCompra, int idMaterial, decimal cantidad)
    {
        var dto = new KardexSalidaCreateDto
        {
            IdCompra = idCompra,
            IdMaterial = idMaterial,
            IdEspecialidad = SeedIds.EspecialidadAlbanileria,  // se autocompletaria si viniera null
            FechaMovimiento = DateTime.Today,
            CantidadSalida = cantidad,
            Observacion = "Salida de setup"
        };
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas-manuales", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al registrar salida. Body: {await response.Content.ReadAsStringAsync()}");
    }
}
