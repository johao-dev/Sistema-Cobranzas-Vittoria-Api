using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

public sealed class GastosDirectosControllerTests : IntegrationTestBase
{
    [Test]
    public async Task CrearYActualizar_Registrado_ProveedorPuedeSerNull()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var id = await CrearAsync(detalle, moneda, 125.50m);

        var creado = await _client.GetAsync($"/api/contable/gastos-directos/{id}");
        Assert.That(creado.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await creado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("gasto").GetProperty("estado").GetString(), Is.EqualTo("REGISTRADO"));
        Assert.That(body.GetProperty("gasto").GetProperty("idProveedor").ValueKind, Is.EqualTo(JsonValueKind.Null));

        var update = await _client.PutAsJsonAsync($"/api/contable/gastos-directos/{id}",
            Dto(detalle, moneda, 220m, "CONCEPTO ACTUALIZADO"));
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await DbHelpers.QueryScalarAsync<decimal>(
            "SELECT Monto FROM contable.GastoDirecto WHERE IdGastoDirecto=@id", new { id }), Is.EqualTo(220m));
    }

    [Test]
    public async Task Confirmar_RetryEsIdempotente_YConfirmadoNoSeEdita()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync(monto: 1_000m);
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var id = await CrearAsync(detalle, moneda, 100m);

        Assert.That((await _client.PostAsync($"/api/contable/gastos-directos/{id}/confirmar", null)).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _client.PostAsync($"/api/contable/gastos-directos/{id}/confirmar", null)).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await ContarMovimientosAsync(id, "EJECUCION"), Is.EqualTo(1));
        Assert.That(await DbHelpers.QueryScalarAsync<string>(
            "SELECT Estado FROM contable.GastoDirecto WHERE IdGastoDirecto=@id", new { id }), Is.EqualTo("CONFIRMADO"));

        var update = await _client.PutAsJsonAsync($"/api/contable/gastos-directos/{id}",
            Dto(detalle, moneda, 101m));
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Confirmar_MonedaIncorrecta_RollbackCompleto()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var usd = await DbHelpers.QueryScalarAsync<int>("SELECT IdMoneda FROM maestra.Moneda WHERE Codigo='USD'");
        var id = await CrearAsync(detalle, usd, 100m);

        var response = await _client.PostAsync($"/api/contable/gastos-directos/{id}/confirmar", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await DbHelpers.QueryScalarAsync<string>(
            "SELECT Estado FROM contable.GastoDirecto WHERE IdGastoDirecto=@id", new { id }), Is.EqualTo("REGISTRADO"));
        Assert.That(await ContarMovimientosAsync(id), Is.Zero);
    }

    [Test]
    public async Task Crear_PartidaInexistente_RechazaSinPersistir()
    {
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var antes = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM contable.GastoDirecto");

        var response = await _client.PostAsJsonAsync("/api/contable/gastos-directos",
            Dto(int.MaxValue, moneda, 100m));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM contable.GastoDirecto"), Is.EqualTo(antes));
    }

    [Test]
    public async Task Confirmar_SaldoInsuficiente_RollbackCompleto()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync(monto: 50m);
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var id = await CrearAsync(detalle, moneda, 51m);

        var response = await _client.PostAsync($"/api/contable/gastos-directos/{id}/confirmar", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await ContarMovimientosAsync(id), Is.Zero);
        Assert.That(await DbHelpers.QueryScalarAsync<string>(
            "SELECT Estado FROM contable.GastoDirecto WHERE IdGastoDirecto=@id", new { id }), Is.EqualTo("REGISTRADO"));
    }

    [Test]
    public async Task Anular_Registrado_NoMueveLedger_YNoPuedeConfirmarse()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var id = await CrearAsync(detalle, moneda, 100m);

        Assert.That((await _client.PostAsync($"/api/contable/gastos-directos/{id}/anular", null)).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await ContarMovimientosAsync(id), Is.Zero);
        Assert.That((await _client.PostAsync($"/api/contable/gastos-directos/{id}/confirmar", null)).StatusCode,
            Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Anular_Confirmado_RetryCreaUnAjusteDecrementoEnDetalleOriginal()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var id = await CrearAsync(detalle, moneda, 100m);
        await _client.PostAsync($"/api/contable/gastos-directos/{id}/confirmar", null);

        await using (var cn = await DbHelpers.OpenTestConnectionAsync())
            await cn.ExecuteAsync("""
                UPDATE pv SET IdEstadoPresupuesto=e.IdEstadoPresupuesto
                FROM ControlPresupuestario.PresupuestoVersion pv
                JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion
                CROSS JOIN ControlPresupuestario.EstadoPresupuesto e
                WHERE pd.IdPresupuestoDetalle=@detalle AND e.Codigo='HISTORICO'
                """, new { detalle });

        Assert.That((await _client.PostAsync($"/api/contable/gastos-directos/{id}/anular", null)).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _client.PostAsync($"/api/contable/gastos-directos/{id}/anular", null)).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        var ajuste = (await DbHelpers.QueryAsync<AjusteRow>("""
            SELECT mp.IdPresupuestoDetalle, mp.Afectacion, mp.Direccion
            FROM ControlPresupuestario.MovimientoPresupuestal mp
            JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
              ON tm.IdTipoMovimientoPresupuestal=mp.IdTipoMovimientoPresupuestal
            WHERE mp.Origen='GASTO_DIRECTO' AND mp.IdOrigen=@id AND tm.Codigo='AJUSTE'
            """, new { id })).Single();
        Assert.That(ajuste.IdPresupuestoDetalle, Is.EqualTo(detalle));
        Assert.That(ajuste.Afectacion, Is.EqualTo("EJECUCION"));
        Assert.That(ajuste.Direccion, Is.EqualTo("DECREMENTO"));
        Assert.That(await ContarMovimientosAsync(id, "AJUSTE"), Is.EqualTo(1));
    }

    [Test]
    public async Task Confirmar_DetalleHistoricoParaOperacionNueva_Rechaza()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var id = await CrearAsync(detalle, moneda, 10m);
        await using (var cn = await DbHelpers.OpenTestConnectionAsync())
            await cn.ExecuteAsync("""
                UPDATE pv SET IdEstadoPresupuesto=e.IdEstadoPresupuesto
                FROM ControlPresupuestario.PresupuestoVersion pv
                JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion
                CROSS JOIN ControlPresupuestario.EstadoPresupuesto e
                WHERE pd.IdPresupuestoDetalle=@detalle AND e.Codigo='HISTORICO'
                """, new { detalle });

        Assert.That((await _client.PostAsync($"/api/contable/gastos-directos/{id}/confirmar", null)).StatusCode,
            Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await ContarMovimientosAsync(id), Is.Zero);
    }

    [Test]
    public async Task Documentos_RegistraFacturaYPago()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var id = await CrearAsync(detalle, moneda, 10m);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Pago"), "tipoDocumento");
        form.Add(new ByteArrayContent("%PDF-1.4 test"u8.ToArray()), "files", "pago.pdf");

        var response = await _client.PostAsync($"/api/contable/gastos-directos/{id}/documentos", form);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await DbHelpers.QueryScalarAsync<int>("""
            SELECT COUNT(*) FROM contable.GastoDirectoDocumento
            WHERE IdGastoDirecto=@id AND TipoDocumento='Pago'
            """, new { id }), Is.EqualTo(1));
    }

    private async Task<int> CrearAsync(int detalle, int moneda, decimal monto)
    {
        var response = await _client.PostAsJsonAsync("/api/contable/gastos-directos",
            Dto(detalle, moneda, monto));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idGastoDirecto").GetInt32();
    }

    private static GastoDirectoUpsertDto Dto(int detalle, int moneda, decimal monto,
        string concepto = "SERVICIO DIRECTO") => new()
    {
        IdPresupuestoDetalle = detalle,
        IdMoneda = moneda,
        Fecha = DateTime.Today,
        Concepto = concepto,
        Descripcion = "Prueba de integración",
        Monto = monto
    };

    private static Task<int> ContarMovimientosAsync(int id, string? tipo = null)
        => DbHelpers.QueryScalarAsync<int>("""
            SELECT COUNT(*)
            FROM ControlPresupuestario.MovimientoPresupuestal mp
            JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
              ON tm.IdTipoMovimientoPresupuestal=mp.IdTipoMovimientoPresupuestal
            WHERE mp.Origen='GASTO_DIRECTO' AND mp.IdOrigen=@id
              AND (@tipo IS NULL OR tm.Codigo=@tipo)
            """, new { id, tipo });

    private sealed class AjusteRow
    {
        public int IdPresupuestoDetalle { get; set; }
        public string Afectacion { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
    }
}
