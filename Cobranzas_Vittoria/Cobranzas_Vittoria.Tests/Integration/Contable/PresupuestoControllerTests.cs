using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

/// <summary>
/// Pruebas de PresupuestoController.
///
///   GET  /api/contable/presupuesto/{idProyecto}   -> presupuesto del proyecto + 15 items fijos
///                                                    + auto-calculo desde gastos + saldo
///   POST /api/contable/presupuesto                -> upsert (reemplaza items del proyecto)
///
/// El controller tiene SQL inline (sin service/repo) y crea las tablas en el primer GET
/// si no existen (EnsureTablesAsync). Como Respawn limpia contable.PresupuestoProyecto*,
/// cada test arranca con BD limpia.
///
/// Items fijos devueltos por el GET (15 conceptos):
///   TERRENO, ALCABALA, CONSTRUCCION, UTILIDAD DEL CONSTRUCTOR, DEMOLICION,
///   ANTEPROYECTO, PROYECTO, LICENCIA DE CONSTRUCCION, GASTOS ADMINISTRATIVOS,
///   PUBLICIDAD / COMISION POR VENTAS, INSTALACIONES (LUZ Y AGUA),
///   CONFORMIDAD DE OBRA, DECLARATORIA DE FABRICA, INDEPENDIZACION, OTROS GASTOS.
///
/// Validaciones del POST:
///   - IdProyecto &lt;= 0  -> 400
///   - Items vacio       -> 400
///   - Item sin concepto -> 400
///
/// El controller retorna DapperRows (PascalCase), por lo que los asserts usan JsonHelpers.
/// </summary>
public class PresupuestoControllerTests : IntegrationTestBase
{
    private const int IdProveedor = 2;            // ACG EDIFICACIONES EIRL
    private const int IdMaterialAlbanileria = 2;  // Material del seed

    // ---- Helpers compartidos ----

    /// <summary>
    /// Crea el flujo Requerimiento -> OC -> Compra en el proyecto 10 (Mayta Capac II).
    /// Retorna el idCompra. El MontoTotal de la Compra = cantidad * precioUnitario.
    /// </summary>
    private async Task<int> CrearCompraEnProyectoAsync(decimal precioUnitario = 20m, decimal cantidad = 10m)
    {
        var idReq = await RequerimientoBuilder.Nuevo()
            .ConItem(IdMaterialAlbanileria, cantidad)
            .CrearEnviadoOcAsync(_client);

        var idOc = await CrearOrdenAsync(idReq, precioUnitario, cantidad);
        var idCompra = await CrearCompraAsync(idOc, precioUnitario, cantidad);
        return idCompra;
    }

    private async Task<int> CrearOrdenAsync(int idRequerimiento, decimal precioUnitario, decimal cantidad)
    {
        var dto = new OrdenCompraCreateDto
        {
            NumeroOrdenCompra = string.Empty,
            IdRequerimiento = idRequerimiento,
            IdProveedor = IdProveedor,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            FechaOrdenCompra = DateTime.Today,
            Descripcion = "OC para test de Presupuesto",
            IdUsuarioCreacion = SeedIds.IngenieroId,
            Items = new List<OrdenCompraDetalleCreateDto>
            {
                new() { IdMaterial = IdMaterialAlbanileria, Cantidad = cantidad, IdProveedor = IdProveedor, PrecioUnitario = precioUnitario }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/compras/ordenes-compra", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear OC. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(body, "idOrdenCompra");
    }

    private async Task<int> CrearCompraAsync(int idOc, decimal precioUnitario, decimal cantidad)
    {
        var dto = new CompraCreateDto
        {
            NumeroCompra = string.Empty,
            IdOrdenCompra = idOc,
            IdProveedor = IdProveedor,
            FechaCompra = DateTime.Today,
            IncluyeIGV = false,
            Observacion = "Compra para test de Presupuesto",
            Items = new List<CompraDetalleCreateDto>
            {
                new() { IdMaterial = IdMaterialAlbanileria, Cantidad = cantidad, PrecioUnitario = precioUnitario }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/compras/compras", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear Compra. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(body, "idCompra");
    }

    /// <summary>
    /// Inserta un GastoProyecto directo en BD con Dapper. Usado para verificar el
    /// auto-calculo de items del Presupuesto desde gastos.
    /// </summary>
    private async Task InsertarGastoProyectoAsync(
        int idProyecto,
        string tipoModulo,
        string concepto,
        decimal montoSoles,
        decimal montoDolares = 0m)
    {
        const string sql = @"
INSERT INTO contable.GastoProyecto
(
    TipoModulo, IdProyecto, Fecha, Concepto, Moneda,
    MontoSoles, MontoDolares, Estado, Activo, TipoCambio, FechaCreacion
)
VALUES
(
    @TipoModulo, @IdProyecto, CAST(GETDATE() AS date), @Concepto, 'PEN',
    @MontoSoles, @MontoDolares, 'Activo', 1, 3.41, GETDATE()
);";
        await DbHelpers.QueryScalarAsync<int>(sql, new
        {
            TipoModulo = tipoModulo,
            IdProyecto = idProyecto,
            Concepto = concepto,
            MontoSoles = montoSoles,
            MontoDolares = montoDolares
        });
    }

    // ---- Tests: GET /{idProyecto} ----

    [Test]
    public async Task Get_ConProyectoSinComprasYSinPresupuesto_Retorna15ItemsEnCero()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/contable/presupuesto/{SeedIds.ProyectoMaytaCapacII}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetInt32(body, "IdProyecto"), Is.EqualTo(SeedIds.ProyectoMaytaCapacII));

        // El proyecto no tiene presupuesto creado => idPresupuesto es null
        Assert.That(JsonHelpers.HasProp(body, "idPresupuesto") || JsonHelpers.HasProp(body, "IdPresupuesto"), Is.True);
        // Sin presupuesto y sin compras => totales en 0
        Assert.That(JsonHelpers.GetDecimal(body, "TotalPresupuesto"), Is.EqualTo(0m));
        Assert.That(JsonHelpers.GetDecimal(body, "TotalCompras"), Is.EqualTo(0m));
        Assert.That(JsonHelpers.GetDecimal(body, "Saldo"), Is.EqualTo(0m));

        // El GET siempre retorna los 15 conceptos fijos
        var items = JsonHelpers.GetProp(body, "items");
        Assert.That(items.GetArrayLength(), Is.EqualTo(15));

        // Todos los items deben tener Soles=0 y Dolares=0 (no hay gastos, no hay presupuesto manual)
        foreach (var item in items.EnumerateArray())
        {
            var concepto = JsonHelpers.GetString(item, "Concepto");
            var soles = JsonHelpers.GetDecimal(item, "Soles");
            var dolares = JsonHelpers.GetDecimal(item, "Dolares");
            Assert.That(soles, Is.EqualTo(0m), $"Item '{concepto}' debe tener Soles=0 sin gastos.");
            Assert.That(dolares, Is.EqualTo(0m), $"Item '{concepto}' debe tener Dolares=0 sin gastos.");
        }
    }

    [Test]
    public async Task Get_ConProyectoConCompras_RetornaTotalComprasYSaldoNegativo()
    {
        // Arrange - crear Compra con MontoTotal = 10 * 20 = 200
        var idCompra = await CrearCompraEnProyectoAsync(precioUnitario: 20m, cantidad: 10m);

        // Act
        var response = await _client.GetAsync(
            $"/api/contable/presupuesto/{SeedIds.ProyectoMaytaCapacII}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // totalCompras debe ser 200 (MontoTotal de la Compra creada)
        Assert.That(JsonHelpers.GetDecimal(body, "TotalCompras"), Is.EqualTo(200.00m));

        // totalPresupuesto = 0 (sin presupuesto manual ni gastos que aporten)
        // saldo = 0 - 200 = -200
        Assert.That(JsonHelpers.GetDecimal(body, "TotalPresupuesto"), Is.EqualTo(0m));
        Assert.That(JsonHelpers.GetDecimal(body, "Saldo"), Is.EqualTo(-200.00m),
            "El saldo es negativo porque no hay presupuesto pero si hay compras.");
    }

    [Test]
    public async Task Get_ConGastosProyecto_AutocalculaTerreno()
    {
        // Arrange - insertar GastoProyecto (Terreno / TERRENO) con MontoSoles = 500
        await InsertarGastoProyectoAsync(
            idProyecto: SeedIds.ProyectoMaytaCapacII,
            tipoModulo: "Terreno",
            concepto: "TERRENO",
            montoSoles: 500m);

        // Act
        var response = await _client.GetAsync(
            $"/api/contable/presupuesto/{SeedIds.ProyectoMaytaCapacII}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // El item TERRENO debe tener Soles=500 (autocalculado desde GastoProyecto)
        var items = JsonHelpers.GetProp(body, "items");
        JsonElement? itemTerreno = null;
        foreach (var item in items.EnumerateArray())
        {
            if (JsonHelpers.GetString(item, "Concepto") == "TERRENO")
            {
                itemTerreno = item;
                break;
            }
        }
        Assert.That(itemTerreno.HasValue, Is.True, "Debe existir el item TERRENO en la lista.");
        Assert.That(JsonHelpers.GetDecimal(itemTerreno!.Value, "Soles"), Is.EqualTo(500m),
            "El item TERRENO debe auto-calculare desde GastoProyecto (Terreno / TERRENO).");

        // totalPresupuesto debe ser al menos 500 (por el item TERRENO)
        Assert.That(JsonHelpers.GetDecimal(body, "TotalPresupuesto"), Is.GreaterThanOrEqualTo(500m));
    }

    // ---- Tests: POST / ----

    [Test]
    public async Task Upsert_ConIdProyectoInvalido_RetornaBadRequest()
    {
        // Arrange - IdProyecto=0 viola la validacion inline del controller
        var dto = new PresupuestoProyectoUpsertDto
        {
            IdProyecto = 0,
            Items = new List<PresupuestoProyectoItemDto>
            {
                new() { Concepto = "TERRENO", Soles = 100m, Dolares = 0m }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contable/presupuesto", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            $"Body: {await response.Content.ReadAsStringAsync()}");
    }

    [Test]
    public async Task Upsert_ConItemsValidos_RetornaOkYPersisteEnBD()
    {
        // Arrange
        var dto = new PresupuestoProyectoUpsertDto
        {
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Items = new List<PresupuestoProyectoItemDto>
            {
                new() { Concepto = "terreno",     Soles = 1000m, Dolares = 0m },     // lowercase -> UPPER
                new() { Concepto = "ALcabala",    Soles = 200m,  Dolares = 0m },     // mixed case -> UPPER
                new() { Concepto = "OTROS GASTOS", Soles = 50m,  Dolares = 0m }      // ya en upper
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contable/presupuesto", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetBoolean(body, "ok"), Is.True);
        Assert.That(JsonHelpers.GetInt32(body, "idPresupuesto"), Is.GreaterThan(0));

        // Assert - 2: BD - 3 filas con los conceptos normalizados a UPPER y Soles redondeados
        var filas = (await DbHelpers.QueryAsync<(string Concepto, decimal Soles)>(
            @"SELECT Concepto, Soles
              FROM contable.PresupuestoProyectoDetalle d
              INNER JOIN contable.PresupuestoProyecto p ON p.IdPresupuestoProyecto = d.IdPresupuestoProyecto
              WHERE p.IdProyecto = @p
              ORDER BY d.Orden",
            new { p = SeedIds.ProyectoMaytaCapacII })).ToList();

        Assert.That(filas.Count, Is.EqualTo(3), "Deben existir exactamente 3 filas de detalle.");
        Assert.That(filas[0].Concepto, Is.EqualTo("TERRENO"));
        Assert.That(filas[0].Soles, Is.EqualTo(1000m));
        Assert.That(filas[1].Concepto, Is.EqualTo("ALCABALA"));
        Assert.That(filas[1].Soles, Is.EqualTo(200m));
        Assert.That(filas[2].Concepto, Is.EqualTo("OTROS GASTOS"));
        Assert.That(filas[2].Soles, Is.EqualTo(50m));
    }

    [Test]
    public async Task Upsert_LlamaDosVeces_ReemplazaLosItems()
    {
        // Arrange - primer upsert con 2 items
        var dto1 = new PresupuestoProyectoUpsertDto
        {
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Items = new List<PresupuestoProyectoItemDto>
            {
                new() { Concepto = "TERRENO",     Soles = 100m, Dolares = 0m },
                new() { Concepto = "ALCABALA",    Soles = 50m,  Dolares = 0m }
            }
        };
        // Segundo upsert con 3 items distintos
        var dto2 = new PresupuestoProyectoUpsertDto
        {
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Items = new List<PresupuestoProyectoItemDto>
            {
                new() { Concepto = "DEMOLICION",         Soles = 300m, Dolares = 0m },
                new() { Concepto = "CONSTRUCCION",       Soles = 500m, Dolares = 0m },
                new() { Concepto = "OTROS GASTOS",       Soles = 25m,  Dolares = 0m }
            }
        };

        // Act
        await _client.PostAsJsonAsync("/api/contable/presupuesto", dto1);
        var response2 = await _client.PostAsJsonAsync("/api/contable/presupuesto", dto2);

        // Assert - 1: HTTP 200 en el segundo upsert
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert - 2: BD - solo los 3 items del segundo upsert existen
        var conceptos = (await DbHelpers.QueryAsync<string>(
            @"SELECT d.Concepto
              FROM contable.PresupuestoProyectoDetalle d
              INNER JOIN contable.PresupuestoProyecto p ON p.IdPresupuestoProyecto = d.IdPresupuestoProyecto
              WHERE p.IdProyecto = @p
              ORDER BY d.Orden",
            new { p = SeedIds.ProyectoMaytaCapacII })).ToList();

        Assert.That(conceptos, Is.EqualTo(new[] { "DEMOLICION", "CONSTRUCCION", "OTROS GASTOS" }),
            "El segundo upsert debe reemplazar completamente al primero (DELETE + INSERT).");

        // Assert - 3: TERRENO y ALCABALA ya no existen
        Assert.That(conceptos, Does.Not.Contain("TERRENO"));
        Assert.That(conceptos, Does.Not.Contain("ALCABALA"));
    }
}
