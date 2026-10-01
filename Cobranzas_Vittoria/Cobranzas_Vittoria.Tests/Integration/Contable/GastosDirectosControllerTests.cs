using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
using Cobranzas_Vittoria.Contable.GastosDirectos.Presentation.Dto;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

public sealed class GastosDirectosControllerTests : IntegrationTestBase
{
    // Consultar exige gasto_directo.ver y operar gasto_directo.operar.
    private static readonly string[] PermisosGasto =
        { Permisos.GastoDirecto.Ver, Permisos.GastoDirecto.Operar };

    [SetUp]
    public void UsarTokenConPermisosDeGasto() => UsarToken(PermisosGasto);

    private void UsarToken(IEnumerable<string>? permisos) =>
        _client.DefaultRequestHeaders.Authorization = permisos is null
            ? null
            : new AuthenticationHeaderValue("Bearer", JwtTestTokenFactory.CrearToken(permisos: permisos));

    // ------------------------------------------------------------ seguridad (H-01)

    [Test]
    public async Task SinSesion_Rechaza401()
    {
        UsarToken(null);
        Assert.That((await _client.GetAsync("/api/contable/gastos-directos")).StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await _client.PostAsJsonAsync("/api/contable/gastos-directos", Dto(1, 1, 1m))).StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SoloConsulta_PuedeListarPeroNoOperar()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var id = await CrearAsync(detalle, moneda, 10m);
        UsarToken(new[] { Permisos.GastoDirecto.Ver });

        Assert.That((await _client.GetAsync("/api/contable/gastos-directos")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _client.PostAsJsonAsync("/api/contable/gastos-directos", Dto(detalle, moneda, 10m))).StatusCode,
            Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await _client.PostAsync($"/api/contable/gastos-directos/{id}/confirmar", null)).StatusCode,
            Is.EqualTo(HttpStatusCode.Forbidden));
    }

    // ---------------------------------------------------------------- secciones

    [Test]
    public async Task Seccion_PartidaDeLaSeccion_SeRegistraYSoloApareceEnSuSeccion()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        await AsignarSeccionAsync(detalle, "OTROS");

        var response = await _client.PostAsJsonAsync("/api/contable/gastos-directos",
            Dto(detalle, moneda, 30m, seccion: "OTROS"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idGastoDirecto").GetInt32();

        var otros = await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos?seccion=OTROS");
        var terreno = await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos?seccion=TERRENO");
        Assert.That(otros.EnumerateArray().Any(g => g.GetProperty("idGastoDirecto").GetInt32() == id), Is.True);
        Assert.That(terreno.EnumerateArray().Any(g => g.GetProperty("idGastoDirecto").GetInt32() == id), Is.False);
    }

    [Test]
    public async Task Seccion_PartidaDeOtraSeccion_Rechaza409SinPersistir()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        await AsignarSeccionAsync(detalle, "OTROS");
        var antes = await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM contable.GastoDirecto");

        var response = await _client.PostAsJsonAsync("/api/contable/gastos-directos",
            Dto(detalle, moneda, 30m, seccion: "TERRENO"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("error").GetString(), Is.EqualTo("SECCION_INVALIDA"));
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM contable.GastoDirecto"), Is.EqualTo(antes));
    }

    [Test]
    public async Task Seccion_CentroCostoNoAdmitido_Rechaza409()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        // El detalle es de un centro de costo PROYECTO y ADMINISTRATIVO solo admite áreas.
        await AsignarSeccionAsync(detalle, "ADMINISTRATIVO", tiposAdmitidos: "ADMINISTRACION");

        var response = await _client.PostAsJsonAsync("/api/contable/gastos-directos",
            Dto(detalle, moneda, 30m, seccion: "ADMINISTRATIVO"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString(),
            Does.Contain("centro de costo"));
    }

    [Test]
    public async Task MonedaReferencia_SeGuardaYNoAfectaElMontoEjecutado()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var pen = await DbHelpersMoneda.ObtenerPenAsync();
        var usd = await DbHelpers.QueryScalarAsync<int>("""
            IF NOT EXISTS (SELECT 1 FROM maestra.Moneda WHERE Codigo = 'USD')
                INSERT INTO maestra.Moneda (Codigo, Nombre, Simbolo) VALUES ('USD', N'Dólar', N'US$');
            SELECT IdMoneda FROM maestra.Moneda WHERE Codigo = 'USD';
            """);
        var dto = Dto(detalle, pen, 380m);
        dto.IdMonedaOriginal = usd;
        dto.MontoOriginal = 100m;
        dto.TipoCambio = 3.8m;

        var response = await _client.PostAsJsonAsync("/api/contable/gastos-directos", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idGastoDirecto").GetInt32();
        Assert.That((await _client.PostAsync($"/api/contable/gastos-directos/{id}/confirmar", null)).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        var gasto = (await _client.GetFromJsonAsync<JsonElement>($"/api/contable/gastos-directos/{id}")).GetProperty("gasto");
        Assert.That(gasto.GetProperty("montoOriginal").GetDecimal(), Is.EqualTo(100m));
        Assert.That(gasto.GetProperty("tipoCambio").GetDecimal(), Is.EqualTo(3.8m));
        Assert.That(await DbHelpers.QueryScalarAsync<decimal>("""
            SELECT mp.Monto FROM ControlPresupuestario.MovimientoPresupuestal mp
            WHERE mp.Origen = 'GASTO_DIRECTO' AND mp.IdOrigen = @id
            """, new { id }), Is.EqualTo(380m), "La ejecución usa el monto en la moneda del presupuesto.");

        dto.TipoCambio = null;
        Assert.That((await _client.PostAsJsonAsync("/api/contable/gastos-directos", dto)).StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest), "Referencia incompleta.");
    }

    [Test]
    public async Task FechaTipoCambio_SeGuardaConLaReferenciaYSinEllaSeRechaza()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var pen = await DbHelpersMoneda.ObtenerPenAsync();
        var usd = await DbHelpers.QueryScalarAsync<int>("""
            IF NOT EXISTS (SELECT 1 FROM maestra.Moneda WHERE Codigo = 'USD')
                INSERT INTO maestra.Moneda (Codigo, Nombre, Simbolo) VALUES ('USD', N'Dólar', N'US$');
            SELECT IdMoneda FROM maestra.Moneda WHERE Codigo = 'USD';
            """);
        var dto = Dto(detalle, pen, 380m);
        dto.IdMonedaOriginal = usd;
        dto.MontoOriginal = 100m;
        dto.TipoCambio = 3.8m;
        dto.FechaTipoCambio = new DateTime(2026, 9, 20);

        var response = await _client.PostAsJsonAsync("/api/contable/gastos-directos", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idGastoDirecto").GetInt32();
        var gasto = (await _client.GetFromJsonAsync<JsonElement>($"/api/contable/gastos-directos/{id}")).GetProperty("gasto");
        Assert.That(gasto.GetProperty("fechaTipoCambio").GetDateTime(), Is.EqualTo(new DateTime(2026, 9, 20)));

        var sinReferencia = Dto(detalle, pen, 10m);
        sinReferencia.FechaTipoCambio = new DateTime(2026, 9, 20);
        Assert.That((await _client.PostAsJsonAsync("/api/contable/gastos-directos", sinReferencia)).StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest), "La fecha del tipo de cambio exige la moneda original.");
    }

    [Test]
    public async Task Proveedores_PrimeroLosDeLaSeccionPorSuCategoriaAntigua()
    {
        // Proveedor antiguo de gasto administrativo con categoría GASTOS MUNICIPALES, migrado al catálogo único.
        await AsignarSeccionAsync(await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync(), "MUNICIPAL");
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var idProveedor = await DbHelpers.QueryScalarAsync<int>("""
            IF NOT EXISTS (SELECT 1 FROM maestra.CategoriaGasto WHERE Nombre = N'GASTOS MUNICIPALES')
                INSERT INTO maestra.CategoriaGasto (Nombre, Activo) VALUES (N'GASTOS MUNICIPALES', 1);
            DECLARE @cat INT = (SELECT TOP (1) IdCategoriaGasto FROM maestra.CategoriaGasto WHERE Nombre = N'GASTOS MUNICIPALES');
            INSERT INTO ControlPresupuestario.SeccionGastoCategoriaGasto (IdSeccionGasto, IdCategoriaGasto)
            SELECT s.IdSeccionGasto, @cat FROM ControlPresupuestario.SeccionGasto s
            WHERE s.Codigo = 'MUNICIPAL'
              AND NOT EXISTS (SELECT 1 FROM ControlPresupuestario.SeccionGastoCategoriaGasto x WHERE x.IdCategoriaGasto = @cat);
            INSERT INTO maestra.ProveedorGastoAdministrativo (RazonSocial, Activo, IdCategoriaGasto)
            VALUES (@razon, 1, @cat);
            DECLARE @legacy INT = CONVERT(INT, SCOPE_IDENTITY());
            INSERT INTO maestra.Proveedor (RazonSocial, Activo) VALUES (@razon, 1);
            DECLARE @nuevo INT = CONVERT(INT, SCOPE_IDENTITY());
            INSERT INTO maestra.ProveedorLegacyMap (Origen, IdLegacy, IdProveedor, Criterio)
            VALUES ('PROVEEDOR_GASTO_ADMIN', @legacy, @nuevo, 'IDENTIDAD_NUEVA');
            SELECT @nuevo;
            """, new { razon = "ZZ MUNICIPAL " + sufijo });

        var municipal = await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos/proveedores?seccion=MUNICIPAL");
        var marketing = await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos/proveedores?seccion=MARKETING_VENTAS");

        var enMunicipal = municipal.EnumerateArray().First(p => p.GetProperty("idProveedor").GetInt32() == idProveedor);
        Assert.That(enMunicipal.GetProperty("deLaSeccion").GetBoolean(), Is.True);
        Assert.That(municipal.EnumerateArray().First().GetProperty("deLaSeccion").GetBoolean(), Is.True,
            "Los de la sección van primero.");
        var enMarketing = marketing.EnumerateArray().First(p => p.GetProperty("idProveedor").GetInt32() == idProveedor);
        Assert.That(enMarketing.GetProperty("deLaSeccion").GetBoolean(), Is.False,
            "En otra sección sigue disponible, pero no como propio.");
    }

    /// <summary>Crea las secciones (Respawn las borra) y pone la partida del detalle en la sección indicada.</summary>
    private static async Task AsignarSeccionAsync(int detalle, string seccion, string tiposAdmitidos = "PROYECTO")
    {
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        await cn.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.SeccionGasto (Codigo, Nombre, Orden)
            SELECT v.Codigo, v.Codigo, v.Orden FROM (VALUES ('ADMINISTRATIVO', 1), ('TERRENO', 2),
                ('MARKETING_VENTAS', 3), ('OTROS', 4), ('MUNICIPAL', 5)) v(Codigo, Orden)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.SeccionGasto s WHERE s.Codigo = v.Codigo);

            IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = 'ADMINISTRACION')
                INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo, Nombre) VALUES ('ADMINISTRACION', N'Administración');

            INSERT INTO ControlPresupuestario.SeccionGastoTipoCentroCosto (IdSeccionGasto, IdTipoCentroCosto)
            SELECT s.IdSeccionGasto, t.IdTipoCentroCosto
            FROM ControlPresupuestario.SeccionGasto s
            JOIN ControlPresupuestario.TipoCentroCosto t ON t.Codigo = @tiposAdmitidos
            WHERE s.Codigo = @seccion AND NOT EXISTS (
                SELECT 1 FROM ControlPresupuestario.SeccionGastoTipoCentroCosto x
                WHERE x.IdSeccionGasto = s.IdSeccionGasto AND x.IdTipoCentroCosto = t.IdTipoCentroCosto);

            UPDATE cp SET IdSeccionGasto = (SELECT IdSeccionGasto FROM ControlPresupuestario.SeccionGasto WHERE Codigo = @seccion)
            FROM ControlPresupuestario.CatalogoPartida cp
            JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdCatalogoPartida = cp.IdCatalogoPartida
            WHERE pd.IdPresupuestoDetalle = @detalle;
            """, new { detalle, seccion, tiposAdmitidos });
    }

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

    private static GastoDirectoUpsertRequest Dto(int detalle, int moneda, decimal monto,
        string concepto = "SERVICIO DIRECTO", string? seccion = null) => new()
    {
        IdPresupuestoDetalle = detalle,
        IdMoneda = moneda,
        Fecha = DateTime.Today,
        Concepto = concepto,
        Descripcion = "Prueba de integración",
        Monto = monto,
        Seccion = seccion
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
