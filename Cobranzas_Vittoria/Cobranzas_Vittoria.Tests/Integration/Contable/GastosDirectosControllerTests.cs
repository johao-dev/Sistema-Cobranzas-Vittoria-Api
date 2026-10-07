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

    // ------------------------------------------------------------ categoría descriptiva sin acoplamiento presupuestario

    [Test]
    public async Task Crear_ExigeCategoriaYObtenerDevuelveSuIdentidadEstable()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();

        var response = await _client.PostAsJsonAsync("/api/contable/gastos-directos", Dto(detalle, moneda, 30m));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idGastoDirecto").GetInt32();
        var gasto = (await _client.GetFromJsonAsync<JsonElement>($"/api/contable/gastos-directos/{id}"))
            .GetProperty("gasto");
        Assert.That(gasto.GetProperty("idPresupuestoDetalle").GetInt32(), Is.EqualTo(detalle));
        Assert.That(gasto.GetProperty("idCategoriaGasto").GetInt32(), Is.EqualTo(1));
        Assert.That(gasto.GetProperty("codigoCategoriaGasto").GetString(), Is.EqualTo("OTROS"));
        Assert.That(gasto.GetProperty("nombreCategoriaGasto").GetString(), Is.EqualTo("OTROS GASTOS"));
    }

    [Test]
    public async Task Crear_CategoriaAusenteInexistenteOInactiva_RechazaSinPersistir()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var inactiva = await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO maestra.CategoriaGasto (Codigo, Nombre, Activo)
            VALUES (@codigo, N'Categoría inactiva de prueba', 0);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { codigo = "INACTIVA_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant() });
        var antes = await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM contable.GastoDirecto");

        var ausente = Dto(detalle, moneda, 10m, idCategoriaGasto: 0);
        var inexistente = Dto(detalle, moneda, 10m, idCategoriaGasto: int.MaxValue);
        var desactivada = Dto(detalle, moneda, 10m, idCategoriaGasto: inactiva);

        Assert.That((await _client.PostAsJsonAsync("/api/contable/gastos-directos", ausente)).StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await _client.PostAsJsonAsync("/api/contable/gastos-directos", inexistente)).StatusCode,
            Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await _client.PostAsJsonAsync("/api/contable/gastos-directos", desactivada)).StatusCode,
            Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM contable.GastoDirecto"), Is.EqualTo(antes));
    }

    [Test]
    public async Task Categoria_NoRestringeCentroPartidaNiProveedor()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var marketing = await IdCategoriaAsync("MARKETING_VENTAS");
        var administrativo = await IdCategoriaAsync("ADMINISTRATIVO");
        var proveedor = await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO maestra.Proveedor (RazonSocial, Activo) VALUES (@nombre, 1);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { nombre = "PROVEEDOR LIBRE " + Guid.NewGuid().ToString("N")[..8] });

        var imputacion = (await DbHelpers.QueryAsync<(string TipoCentroCosto, string Partida)>("""
            SELECT tcc.Codigo AS TipoCentroCosto, cp.Nombre AS Partida
            FROM ControlPresupuestario.PresupuestoDetalle pd
            JOIN ControlPresupuestario.CatalogoPartida cp ON cp.IdCatalogoPartida=pd.IdCatalogoPartida
            JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuestoVersion=pd.IdPresupuestoVersion
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto=pv.IdPresupuesto
            JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto=p.IdCentroCosto
            JOIN ControlPresupuestario.TipoCentroCosto tcc ON tcc.IdTipoCentroCosto=cc.IdTipoCentroCosto
            WHERE pd.IdPresupuestoDetalle=@detalle
            """, new { detalle })).Single();
        Assert.That(imputacion.TipoCentroCosto, Is.EqualTo("PROYECTO"),
            "La categoría MARKETING_VENTAS se prueba contra un centro que no es de marketing/ventas.");
        Assert.That(imputacion.Partida, Does.Not.Contain("Administrativ").IgnoreCase,
            "La categoría ADMINISTRATIVO se prueba contra una partida no administrativa.");

        var marketingDto = Dto(detalle, moneda, 20m, "MARKETING EN CUALQUIER CENTRO", marketing);
        marketingDto.IdProveedor = proveedor;
        var administrativoDto = Dto(detalle, moneda, 21m, "ADMINISTRATIVO EN PARTIDA VÁLIDA", administrativo);

        Assert.That((await _client.PostAsJsonAsync("/api/contable/gastos-directos", marketingDto)).StatusCode,
            Is.EqualTo(HttpStatusCode.Created));
        Assert.That((await _client.PostAsJsonAsync("/api/contable/gastos-directos", administrativoDto)).StatusCode,
            Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Categorias_DevuelveSoloActivasConContratoCanonico()
    {
        await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO maestra.CategoriaGasto (Codigo, Nombre, Activo)
            VALUES (@codigo, N'No visible', 0);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { codigo = "NO_VISIBLE_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant() });

        var categorias = await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos/categorias");

        Assert.That(categorias.EnumerateArray().All(c => c.GetProperty("codigo").GetString() != null), Is.True);
        Assert.That(categorias.EnumerateArray().Any(c => c.GetProperty("codigo").GetString() == "MARKETING_VENTAS"), Is.True);
        Assert.That(categorias.EnumerateArray().Any(c => c.GetProperty("nombre").GetString() == "No visible"), Is.False);
        Assert.That(categorias.EnumerateArray().First().EnumerateObject().Select(p => p.Name),
            Is.EquivalentTo(new[] { "idCategoriaGasto", "codigo", "nombre" }));
    }

    [Test]
    public async Task InstalacionLimpia_TerminaConCategoriaYContratoFisicoCompleto()
    {
        Assert.That(await DbHelpers.QueryScalarAsync<int>("""
            SELECT COUNT(*) FROM maestra.CategoriaGasto
            WHERE Codigo IS NULL OR Codigo=''
            """), Is.Zero);
        Assert.That(await DbHelpers.QueryScalarAsync<int>("""
            SELECT is_nullable FROM sys.columns
            WHERE object_id=OBJECT_ID('contable.GastoDirecto') AND name='IdCategoriaGasto'
            """), Is.Zero);
        Assert.That(await DbHelpers.QueryScalarAsync<int>("""
            SELECT COUNT(*) FROM sys.foreign_keys
            WHERE parent_object_id=OBJECT_ID('contable.GastoDirecto')
              AND name='FK_GastoDirecto_CategoriaGasto'
            """), Is.EqualTo(1));
        Assert.That(await DbHelpers.QueryScalarAsync<int>("""
            SELECT COUNT(*) FROM sys.table_types tt
            JOIN sys.columns c ON c.object_id=tt.type_table_object_id
            WHERE SCHEMA_NAME(tt.schema_id)='maestra' AND tt.name='TVP_CategoriaGasto' AND c.name='Codigo'
            """), Is.EqualTo(1));
    }

    [Test]
    public async Task CentrosCosto_DevuelveActivosSinFiltroDePantalla()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var centro = await DbHelpers.QueryScalarAsync<int>("""
            SELECT p.IdCentroCosto FROM ControlPresupuestario.PresupuestoDetalle d
            JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuestoVersion=d.IdPresupuestoVersion
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto=v.IdPresupuesto
            WHERE d.IdPresupuestoDetalle=@detalle
            """, new { detalle });

        var centros = await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos/centros-costo");

        Assert.That(centros.EnumerateArray().Any(c => c.GetProperty("idCentroCosto").GetInt32() == centro), Is.True);
    }

    [Test]
    public async Task PartidasDisponibles_UsaCentroYDevuelveDetalleEconomicamenteValido()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var centro = await DbHelpers.QueryScalarAsync<int>("""
            SELECT p.IdCentroCosto FROM ControlPresupuestario.PresupuestoDetalle d
            JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuestoVersion=d.IdPresupuestoVersion
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto=v.IdPresupuesto
            WHERE d.IdPresupuestoDetalle=@detalle
            """, new { detalle });

        var partidas = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/contable/gastos-directos/partidas-disponibles?idCentroCosto={centro}");

        Assert.That(partidas.EnumerateArray().Any(p =>
            p.GetProperty("idPresupuestoDetalle").GetInt32() == detalle), Is.True);
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
    public async Task Proveedores_DevuelveCanonicosActivosSinMarcaLegacy()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var idProveedor = await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO maestra.Proveedor (RazonSocial, Activo) VALUES (@razon, 1);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { razon = "ZZ CANONICO " + sufijo });

        var proveedores = await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos/proveedores");
        var proveedor = proveedores.EnumerateArray().Single(p => p.GetProperty("idProveedor").GetInt32() == idProveedor);
        Assert.That(proveedor.GetProperty("razonSocial").GetString(), Does.Contain(sufijo));
        Assert.That(proveedor.EnumerateObject().Select(p => p.Name),
            Is.EquivalentTo(new[] { "idProveedor", "razonSocial", "ruc" }));
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

        var nuevaCategoria = await IdCategoriaAsync("MARKETING_VENTAS");
        var update = await _client.PutAsJsonAsync($"/api/contable/gastos-directos/{id}",
            Dto(detalle, moneda, 220m, "CONCEPTO ACTUALIZADO", nuevaCategoria));
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await DbHelpers.QueryScalarAsync<decimal>(
            "SELECT Monto FROM contable.GastoDirecto WHERE IdGastoDirecto=@id", new { id }), Is.EqualTo(220m));
        Assert.That(await DbHelpers.QueryScalarAsync<int>(
            "SELECT IdCategoriaGasto FROM contable.GastoDirecto WHERE IdGastoDirecto=@id", new { id }),
            Is.EqualTo(nuevaCategoria));
        Assert.That(await ContarMovimientosAsync(id), Is.Zero,
            "Cambiar la categoría de un REGISTRADO no debe afectar el ledger.");
    }

    [Test]
    public async Task Listado_FiltraUnaOVariasCategorias_YSinFiltroDevuelveTodas()
    {
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var moneda = await DbHelpersMoneda.ObtenerPenAsync();
        var otros = await IdCategoriaAsync("OTROS");
        var marketing = await IdCategoriaAsync("MARKETING_VENTAS");
        var terreno = await IdCategoriaAsync("TERRENO");
        await CrearAsync(detalle, moneda, 11m, otros);
        await CrearAsync(detalle, moneda, 12m, marketing);
        await CrearAsync(detalle, moneda, 13m, terreno);

        var una = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/contable/gastos-directos?idCategoriaGasto={marketing}");
        var varias = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/contable/gastos-directos?idCategoriaGasto={otros}&idCategoriaGasto={terreno}");
        var todas = await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos");

        Assert.That(una.EnumerateArray().Select(g => g.GetProperty("idCategoriaGasto").GetInt32()),
            Is.EquivalentTo(new[] { marketing }));
        Assert.That(varias.EnumerateArray().Select(g => g.GetProperty("idCategoriaGasto").GetInt32()),
            Is.EquivalentTo(new[] { otros, terreno }));
        Assert.That(varias.EnumerateArray().All(g =>
            g.GetProperty("codigoCategoriaGasto").ValueKind == JsonValueKind.String &&
            g.GetProperty("nombreCategoriaGasto").ValueKind == JsonValueKind.String), Is.True);
        Assert.That(todas.GetArrayLength(), Is.EqualTo(3));
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
        var update = Dto(detalle, moneda, 101m, idCategoriaGasto: await IdCategoriaAsync("MARKETING_VENTAS"));
        Assert.That((await _client.PutAsJsonAsync($"/api/contable/gastos-directos/{id}", update)).StatusCode,
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

    private async Task<int> CrearAsync(int detalle, int moneda, decimal monto, int idCategoriaGasto = 1)
    {
        var response = await _client.PostAsJsonAsync("/api/contable/gastos-directos",
            Dto(detalle, moneda, monto, idCategoriaGasto: idCategoriaGasto));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idGastoDirecto").GetInt32();
    }

    private static GastoDirectoUpsertRequest Dto(int detalle, int moneda, decimal monto,
        string concepto = "SERVICIO DIRECTO", int idCategoriaGasto = 1) => new()
    {
        IdPresupuestoDetalle = detalle,
        IdCategoriaGasto = idCategoriaGasto,
        IdMoneda = moneda,
        Fecha = DateTime.Today,
        Concepto = concepto,
        Descripcion = "Prueba de integración",
        Monto = monto
    };

    private static Task<int> IdCategoriaAsync(string codigo)
        => DbHelpers.QueryScalarAsync<int>(
            "SELECT IdCategoriaGasto FROM maestra.CategoriaGasto WHERE Codigo=@codigo", new { codigo });

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
