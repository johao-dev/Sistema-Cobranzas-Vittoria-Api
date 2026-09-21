using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

[NonParallelizable]
public class IntegracionEconomicaComprasTests : IntegrationTestBase
{
    private const int IdMaterial = 2;
    private const int IdProveedor = 2;

    [Test]
    public async Task FlujoDefinitivo_ComprometeAceptaAtiendeYCierra_SinDuplicarEventos()
    {
        var flujo = await CrearOrdenAsync(montoPresupuestado: 1_000m, precioOc: 10m, aprobar: true);

        var idCompra = await CrearCompraAsync(flujo.IdOrdenCompra, precioReal: 12m);
        Assert.That(await EstadoCompra(idCompra), Is.EqualTo("REGISTRADA"));
        Assert.That(await CantidadMovimientos(), Is.EqualTo(1));
        Assert.That(await CantidadKardex(idCompra), Is.Zero);

        var aceptar = await AceptarCompraAsync(idCompra);
        Assert.That(aceptar.StatusCode, Is.EqualTo(HttpStatusCode.OK), await aceptar.Content.ReadAsStringAsync());
        Assert.That(await EstadoCompra(idCompra), Is.EqualTo("ACEPTADA"));
        Assert.That(await EstadoOc(flujo.IdOrdenCompra), Is.EqualTo("ATENDIDA"));
        Assert.That(await CantidadKardex(idCompra), Is.EqualTo(1));

        var movimientos = (await DbHelpers.QueryAsync<MovimientoRow>("""
            SELECT tm.Codigo AS Tipo, mp.Monto
            FROM ControlPresupuestario.MovimientoPresupuestal mp
            JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
                ON tm.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
            ORDER BY mp.IdMovimientoPresupuestal;
            """)).ToArray();
        Assert.That(movimientos.Select(x => (x.Tipo, x.Monto)), Is.EquivalentTo(new[]
        {
            ("COMPROMISO", 100m),
            ("LIBERACION", 100m),
            ("EJECUCION", 120m)
        }));

        var retry = await AceptarCompraAsync(idCompra);
        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await CantidadMovimientos(), Is.EqualTo(3));
        Assert.That(await CantidadKardex(idCompra), Is.EqualTo(1));

        var cerrar = await CambiarEstadoAsync(flujo.IdOrdenCompra, "CERRADA");
        Assert.That(cerrar.StatusCode, Is.EqualTo(HttpStatusCode.OK), await cerrar.Content.ReadAsStringAsync());
        Assert.That(await EstadoOc(flujo.IdOrdenCompra), Is.EqualTo("CERRADA"));

        var anular = await CambiarEstadoAsync(flujo.IdOrdenCompra, "ANULADA");
        Assert.That(anular.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await EstadoOc(flujo.IdOrdenCompra), Is.EqualTo("CERRADA"));
    }

    [Test]
    public async Task EditarAprobada_AumentaYDisminuyeCompromiso_AnularLiberaSaldoActual()
    {
        var flujo = await CrearOrdenAsync(montoPresupuestado: 1_000m, precioOc: 10m, aprobar: true);

        var aumentar = await ActualizarOrdenAsync(flujo, precio: 15m);
        Assert.That(aumentar.StatusCode, Is.EqualTo(HttpStatusCode.OK), await aumentar.Content.ReadAsStringAsync());
        var disminuir = await ActualizarOrdenAsync(flujo, precio: 8m);
        Assert.That(disminuir.StatusCode, Is.EqualTo(HttpStatusCode.OK), await disminuir.Content.ReadAsStringAsync());

        var anular = await CambiarEstadoAsync(flujo.IdOrdenCompra, "ANULADA");
        Assert.That(anular.StatusCode, Is.EqualTo(HttpStatusCode.OK), await anular.Content.ReadAsStringAsync());
        Assert.That(await EstadoOc(flujo.IdOrdenCompra), Is.EqualTo("ANULADA"));

        var totales = (await DbHelpers.QueryAsync<MovimientoRow>("""
            SELECT tm.Codigo AS Tipo, SUM(mp.Monto) AS Monto
            FROM ControlPresupuestario.MovimientoPresupuestal mp
            JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
                ON tm.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
            GROUP BY tm.Codigo;
            """)).ToDictionary(x => x.Tipo, x => x.Monto);
        Assert.That(totales["COMPROMISO"], Is.EqualTo(150m));
        Assert.That(totales["LIBERACION"], Is.EqualTo(150m));
        Assert.That(await DbHelpers.QueryScalarAsync<int>(
            "SELECT VersionEconomica FROM compras.OrdenCompra WHERE IdOrdenCompra=@id",
            new { id = flujo.IdOrdenCompra }), Is.EqualTo(2));
    }

    [Test]
    public async Task AprobarSinSaldo_RollbackCompletoMantieneOcRegistrada()
    {
        var flujo = await CrearOrdenAsync(montoPresupuestado: 99m, precioOc: 10m, aprobar: false);

        var respuesta = await CambiarEstadoAsync(flujo.IdOrdenCompra, "APROBADA");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var error = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("error").GetString(), Is.EqualTo("SALDO_PRESUPUESTARIO_INSUFICIENTE"));
        Assert.That(await EstadoOc(flujo.IdOrdenCompra), Is.EqualTo("REGISTRADA"));
        Assert.That(await CantidadMovimientos(), Is.Zero);
        Assert.That(await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM compras.OrdenCompraHistorial WHERE IdOrdenCompra=@id",
            new { id = flujo.IdOrdenCompra }), Is.Zero);
    }

    [Test]
    public async Task AprobarSinPartidaOConMonedaDistinta_RechazaSinMovimiento()
    {
        var sinPartida = await CrearOrdenAsync(montoPresupuestado: null, precioOc: 10m, aprobar: false);
        var respuesta = await CambiarEstadoAsync(sinPartida.IdOrdenCompra, "APROBADA");
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await EstadoOc(sinPartida.IdOrdenCompra), Is.EqualTo("REGISTRADA"));

        await ResetDatabaseBeforeEachTest();
        var monedaDistinta = await CrearOrdenAsync(montoPresupuestado: 1_000m, precioOc: 10m,
            aprobar: false, usarUsd: true);
        respuesta = await CambiarEstadoAsync(monedaDistinta.IdOrdenCompra, "APROBADA");
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await EstadoOc(monedaDistinta.IdOrdenCompra), Is.EqualTo("REGISTRADA"));
        Assert.That(await CantidadMovimientos(), Is.Zero);
    }

    [Test]
    public async Task PartidaHistorica_ConservaOperacionExistente_YNoAdmiteNuevoRequerimiento()
    {
        var idDetalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync(monto: 1_000m);
        var idReq = await RequerimientoBuilder.Nuevo()
            .ConItem(IdMaterial, 10m, idPresupuestoDetalle: idDetalle)
            .CrearEnviadoOcAsync(_client);

        await using (var cn = await DbHelpers.OpenTestConnectionAsync())
        {
            var idPresupuesto = await cn.QuerySingleAsync<int>("""
                SELECT pv.IdPresupuesto FROM ControlPresupuestario.PresupuestoDetalle pd
                JOIN ControlPresupuestario.PresupuestoVersion pv
                    ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
                WHERE pd.IdPresupuestoDetalle = @idDetalle;
                """, new { idDetalle });
            var nueva = await cn.QuerySingleAsync<dynamic>(
                "ControlPresupuestario.usp_PresupuestoVersion_CrearNueva",
                new { IdPresupuesto = idPresupuesto, Descripcion = "V2", MotivoCambio = "Test", UsuarioCreacion = "test" },
                commandType: CommandType.StoredProcedure);
            await cn.QuerySingleAsync("ControlPresupuestario.usp_PresupuestoVersion_Aprobar",
                new { IdPresupuestoVersion = (int)nueva.IdPresupuestoVersion, UsuarioAprobacion = "test" },
                commandType: CommandType.StoredProcedure);
        }

        var idOc = await CrearOcDesdeRequerimientoAsync(idReq, 10m, usarUsd: false);
        var aprobar = await CambiarEstadoAsync(idOc, "APROBADA");
        Assert.That(aprobar.StatusCode, Is.EqualTo(HttpStatusCode.OK), await aprobar.Content.ReadAsStringAsync());

        var nuevo = await RequerimientoBuilder.Nuevo()
            .ConItem(IdMaterial, 10m, idPresupuestoDetalle: idDetalle)
            .CrearAsyncSinAssert(_client);
        Assert.That(nuevo.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task CompraRegistrada_BloqueaCambioIncompatible_PermiteCambioSoloDePrecio()
    {
        var flujo = await CrearOrdenAsync(montoPresupuestado: 1_000m, precioOc: 10m, aprobar: true);
        await CrearCompraAsync(flujo.IdOrdenCompra, precioReal: 11m);

        var compatible = await ActualizarOrdenAsync(flujo, precio: 12m);
        Assert.That(compatible.StatusCode, Is.EqualTo(HttpStatusCode.OK), await compatible.Content.ReadAsStringAsync());

        var incompatible = await _client.PutAsJsonAsync($"/api/compras/ordenes-compra/{flujo.IdOrdenCompra}",
            new OrdenCompraUpdateDto
            {
                NumeroOrdenCompra = flujo.NumeroOrdenCompra,
                IdRequerimiento = flujo.IdRequerimiento,
                IdMoneda = flujo.IdMoneda,
                FechaOrdenCompra = DateTime.Today,
                Items =
                [
                    new() { IdMaterial = IdMaterial, Cantidad = 9m, IdProveedor = IdProveedor, PrecioUnitario = 12m }
                ]
            });
        Assert.That(incompatible.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await DbHelpers.QueryScalarAsync<decimal>(
            "SELECT Cantidad FROM compras.OrdenCompraDetalle WHERE IdOrdenCompra=@id",
            new { id = flujo.IdOrdenCompra }), Is.EqualTo(10m));
    }

    private async Task<FlujoOc> CrearOrdenAsync(decimal? montoPresupuestado, decimal precioOc,
        bool aprobar, bool usarUsd = false)
    {
        int? idDetalle = montoPresupuestado.HasValue
            ? await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync(monto: montoPresupuestado.Value)
            : null;
        var idReq = await RequerimientoBuilder.Nuevo()
            .ConItem(IdMaterial, 10m, idPresupuestoDetalle: idDetalle)
            .CrearEnviadoOcAsync(_client);
        var idOc = await CrearOcDesdeRequerimientoAsync(idReq, precioOc, usarUsd);
        var idMoneda = await DbHelpers.QueryScalarAsync<int>(
            "SELECT IdMoneda FROM compras.OrdenCompra WHERE IdOrdenCompra=@id", new { id = idOc });
        var numero = await DbHelpers.QueryScalarAsync<string>(
            "SELECT NumeroOrdenCompra FROM compras.OrdenCompra WHERE IdOrdenCompra=@id", new { id = idOc });
        if (aprobar)
        {
            var response = await CambiarEstadoAsync(idOc, "APROBADA");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        }
        return new FlujoOc(idReq, idOc, idMoneda, numero!);
    }

    private async Task<int> CrearOcDesdeRequerimientoAsync(int idReq, decimal precio, bool usarUsd)
    {
        var idMoneda = await DbHelpers.QueryScalarAsync<int>(
            "SELECT IdMoneda FROM maestra.Moneda WHERE Codigo=@codigo",
            new { codigo = usarUsd ? "USD" : "PEN" });
        var response = await _client.PostAsJsonAsync("/api/compras/ordenes-compra", new OrdenCompraCreateDto
        {
            IdRequerimiento = idReq,
            IdMoneda = idMoneda,
            FechaOrdenCompra = DateTime.Today,
            IdUsuarioCreacion = SeedIds.IngenieroId,
            Items =
            [
                new() { IdMaterial = IdMaterial, Cantidad = 10m, IdProveedor = IdProveedor, PrecioUnitario = precio }
            ]
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("idOrdenCompra").GetInt32();
    }

    private async Task<int> CrearCompraAsync(int idOc, decimal precioReal)
    {
        var response = await _client.PostAsJsonAsync("/api/compras/compras", new CompraCreateDto
        {
            IdOrdenCompra = idOc,
            FechaCompra = DateTime.Today,
            Items =
            [
                new() { IdMaterial = IdMaterial, Cantidad = 10m, PrecioUnitario = precioReal }
            ]
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("idCompra").GetInt32();
    }

    private Task<HttpResponseMessage> CambiarEstadoAsync(int idOc, string estado) =>
        _client.PatchAsync($"/api/compras/ordenes-compra/{idOc}/estado",
            JsonContent.Create(new OrdenCompraEstadoDto
            {
                EstadoNuevo = estado,
                IdUsuario = SeedIds.IngenieroId,
                Observacion = "Transición de prueba"
            }));

    private Task<HttpResponseMessage> AceptarCompraAsync(int idCompra) =>
        _client.PostAsJsonAsync($"/api/compras/compras/{idCompra}/aceptar",
            new CompraAceptarDto { IdUsuario = SeedIds.IngenieroId, Observacion = "Aceptación de prueba" });

    private Task<HttpResponseMessage> ActualizarOrdenAsync(FlujoOc flujo, decimal precio) =>
        _client.PutAsJsonAsync($"/api/compras/ordenes-compra/{flujo.IdOrdenCompra}",
            new OrdenCompraUpdateDto
            {
                NumeroOrdenCompra = flujo.NumeroOrdenCompra,
                IdRequerimiento = flujo.IdRequerimiento,
                IdMoneda = flujo.IdMoneda,
                FechaOrdenCompra = DateTime.Today,
                Items =
                [
                    new() { IdMaterial = IdMaterial, Cantidad = 10m, IdProveedor = IdProveedor, PrecioUnitario = precio }
                ]
            });

    private static Task<string?> EstadoOc(int id) => DbHelpers.QueryScalarAsync<string>(
        "SELECT Estado FROM compras.OrdenCompra WHERE IdOrdenCompra=@id", new { id });
    private static Task<string?> EstadoCompra(int id) => DbHelpers.QueryScalarAsync<string>(
        "SELECT Estado FROM compras.Compra WHERE IdCompra=@id", new { id });
    private static Task<int> CantidadMovimientos() => DbHelpers.QueryScalarAsync<int>(
        "SELECT COUNT(*) FROM ControlPresupuestario.MovimientoPresupuestal");
    private static Task<int> CantidadKardex(int idCompra) => DbHelpers.QueryScalarAsync<int>(
        "SELECT COUNT(*) FROM almacen.KardexMovimiento WHERE IdCompra=@idCompra", new { idCompra });

    private sealed record FlujoOc(int IdRequerimiento, int IdOrdenCompra, int IdMoneda, string NumeroOrdenCompra);
    private sealed record MovimientoRow(string Tipo, decimal Monto);
}

internal static class RequerimientoBuilderTestExtensions
{
    public static async Task<HttpResponseMessage> CrearAsyncSinAssert(
        this RequerimientoBuilder builder, HttpClient client) =>
        await client.PostAsJsonAsync("/api/compras/requerimientos", builder.Build());
}
