using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Tests.Integration.Common;
using CP = Cobranzas_Vittoria.Seguridad.Authorization.Permisos.ControlPresupuestario;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

/// <summary>Conciliación HTTP/SQL real sobre el contenedor desechable de la suite.</summary>
[NonParallelizable]
public sealed class ConsolidacionPresupuestariaApiTests : IntegrationTestBase
{
    private const string Base = "/api/control-presupuestario";
    private static readonly string[] Reportes = { "resumen", "saldo", "presupuesto-vs-comprometido",
        "presupuesto-vs-ejecutado", "gastos-por-partida", "gastos-por-centro-costo" };
    private int _centro;
    private int _presupuesto;
    private int _partida;
    private int _partidaCero;
    private int _moneda;
    private int _versionActual;
    private int _detalleActual;
    private int _versionHistorica;
    private int _detalleHistorico;

    [SetUp]
    public async Task Preparar()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            JwtTestTokenFactory.CrearToken(permisos: new[] { CP.Reporte.Ver, CP.Movimiento.Ver, CP.Presupuesto.Ver }));
        _moneda = await DbHelpersMoneda.ObtenerPenAsync();
        await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO ControlPresupuestario.EstadoPresupuesto (Codigo, Nombre)
            VALUES ('BORRADOR', 'Borrador'), ('APROBADO', 'Aprobado'), ('HISTORICO', 'Historico'), ('ANULADO', 'Anulado');
            INSERT INTO ControlPresupuestario.TipoMovimientoPresupuestal (Codigo, Nombre)
            VALUES ('COMPROMISO', 'Compromiso'), ('LIBERACION', 'Liberacion'), ('EJECUCION', 'Ejecucion'), ('AJUSTE', 'Ajuste');
            INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo, Nombre) VALUES ('ADMINISTRACION', 'Administracion');
            INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre) VALUES ('MATERIALES', 'Materiales');
            SELECT 1;
            """);
        _centro = await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO ControlPresupuestario.CentroCosto (Codigo, Nombre, IdTipoCentroCosto)
            SELECT 'CC-CONS', 'Centro consolidado', IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto;
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """);
        _partida = await CrearPartida("1");
        _partidaCero = await CrearPartida("2");
        _presupuesto = await CrearPresupuesto("P-CONS", _moneda);
        _versionHistorica = await CrearVersion(_presupuesto, 1, "HISTORICO");
        _detalleHistorico = await CrearDetalle(_versionHistorica, _partida, 1000m);
        int segunda = await CrearVersion(_presupuesto, 2, "HISTORICO");
        int detalleSegundo = await CrearDetalle(segunda, _partida, 1100m);
        _versionActual = await CrearVersion(_presupuesto, 3, "APROBADO");
        _detalleActual = await CrearDetalle(_versionActual, _partida, 1200m);
        await CrearDetalle(_versionActual, _partidaCero, 0m);
        int detalleCeroHistorico = await CrearDetalle(_versionHistorica, _partidaCero, 50m);

        await Movimiento(_detalleHistorico, "COMPROMISO", 400m, "COMPROMISO", "INCREMENTO");
        await Movimiento(_detalleHistorico, "LIBERACION", 100m, "COMPROMISO", "DECREMENTO");
        await Movimiento(_detalleHistorico, "EJECUCION", 300m, "EJECUCION", "INCREMENTO");
        await Movimiento(detalleSegundo, "AJUSTE", 50m, "COMPROMISO", "INCREMENTO");
        await Movimiento(detalleSegundo, "AJUSTE", 25m, "COMPROMISO", "DECREMENTO");
        await Movimiento(detalleSegundo, "AJUSTE", 40m, "EJECUCION", "INCREMENTO");
        await Movimiento(detalleSegundo, "AJUSTE", 10m, "EJECUCION", "DECREMENTO");
        await Movimiento(_detalleActual, "COMPROMISO", 50m, "COMPROMISO", "INCREMENTO");
        await Movimiento(_detalleActual, "EJECUCION", 200m, "EJECUCION", "INCREMENTO");
        // Par compensatorio: se conserva la trazabilidad sin alterar el neto.
        await Movimiento(detalleSegundo, "AJUSTE", 7.25m, "EJECUCION", "INCREMENTO");
        await Movimiento(detalleSegundo, "AJUSTE", 7.25m, "EJECUCION", "DECREMENTO");
        await Movimiento(detalleCeroHistorico, "EJECUCION", 25m, "EJECUCION", "INCREMENTO");
        foreach (string estado in new[] { "BORRADOR", "ANULADO" })
        {
            int version = await CrearVersion(_presupuesto, estado == "BORRADOR" ? 4 : 5, estado);
            int detalle = await CrearDetalle(version, _partida, 9000m);
            await Movimiento(detalle, "EJECUCION", 900m, "EJECUCION", "INCREMENTO");
        }
    }

    [Test]
    public async Task SeisReportes_ConcilianConArbolYConservanBaseVigente()
    {
        JsonElement resumen = await Obtener("resumen");
        JsonElement totales = resumen.GetProperty("totales");
        AssertMontos(totales, 1200m, 375m, 555m, 270m, "presupuestado");
        Assert.That(totales.GetProperty("porcentajeEjecutado").GetDecimal(), Is.EqualTo(46.25m));
        Assert.That(totales.GetProperty("partidasExcedidas").GetInt32(), Is.EqualTo(1));
        JsonElement exceso = resumen.GetProperty("partidasExcedidas").EnumerateArray().Single();
        Assert.That(exceso.GetProperty("montoExcedido").GetDecimal(), Is.EqualTo(25m));
        Assert.That(exceso.GetProperty("idCatalogoPartida").GetInt32(), Is.EqualTo(_partidaCero));

        JsonElement saldo = Fila(await Obtener("saldo"), _partida);
        AssertMontos(saldo, 1200m, 375m, 530m, 295m);
        Assert.That(saldo.GetProperty("idPresupuestoVersion").GetInt32(), Is.EqualTo(_versionActual));
        Assert.That(saldo.GetProperty("idPresupuestoDetalle").GetInt32(), Is.EqualTo(_detalleActual));
        Assert.That(saldo.GetProperty("numeroVersion").GetInt32(), Is.EqualTo(3));
        Assert.That(saldo.GetProperty("estado").GetString(), Is.EqualTo("APROBADO"));

        JsonElement comprometido = Fila(await Obtener("presupuesto-vs-comprometido"), _partida);
        Assert.That(comprometido.GetProperty("comprometido").GetDecimal(), Is.EqualTo(375m));
        Assert.That(comprometido.GetProperty("diferencia").GetDecimal(), Is.EqualTo(825m));
        Assert.That(comprometido.GetProperty("porcentajeComprometido").GetDecimal(), Is.EqualTo(31.25m));
        foreach (string reporte in new[] { "presupuesto-vs-ejecutado", "gastos-por-partida" })
        {
            JsonElement filas = await Obtener(reporte);
            JsonElement ejecutado = Fila(filas, _partida);
            Assert.That(ejecutado.GetProperty("ejecutado").GetDecimal(), Is.EqualTo(530m), reporte);
            Assert.That(ejecutado.GetProperty("diferencia").GetDecimal(), Is.EqualTo(670m), reporte);
            Assert.That(ejecutado.GetProperty("porcentajeEjecutado").GetDecimal(), Is.EqualTo(530m * 100m / 1200m));
            JsonElement cero = Fila(filas, _partidaCero);
            Assert.That(cero.GetProperty("porcentajeEjecutado").GetDecimal(), Is.Zero);
            Assert.That(cero.GetProperty("diferencia").GetDecimal(), Is.EqualTo(-25m));
        }
        JsonElement centro = (await Obtener("gastos-por-centro-costo")).EnumerateArray().Single();
        AssertMontos(centro, 1200m, 375m, 555m, 270m);
        Assert.That(centro.GetProperty("porcentajeEjecutado").GetDecimal(), Is.EqualTo(46.25m));
        JsonElement arbol = await Json($"{Base}/consultas/arbol?idCentroCosto={_centro}&idPresupuesto={_presupuesto}");
        JsonElement totalArbol = arbol.GetProperty("totales");
        Assert.That(totalArbol.GetProperty("montoPresupuestado").GetDecimal(), Is.EqualTo(1200m));
        Assert.That(totalArbol.GetProperty("montoComprometido").GetDecimal(), Is.EqualTo(375m));
        Assert.That(totalArbol.GetProperty("montoEjecutado").GetDecimal(), Is.EqualTo(555m));
        Assert.That(totalArbol.GetProperty("saldoDisponible").GetDecimal(), Is.EqualTo(270m));
    }

    [Test]
    public async Task Historial_IdentificaVersionesYReconstruyeNetoSinDuplicados()
    {
        JsonElement[] movimientos = (await Json(RutaMovimientos(_presupuesto, _partida))).EnumerateArray().ToArray();
        Assert.That(movimientos, Has.Length.EqualTo(11));
        Assert.That(movimientos.Sum(m => m.GetProperty("efectoComprometido").GetDecimal()), Is.EqualTo(375m));
        Assert.That(movimientos.Sum(m => m.GetProperty("efectoEjecutado").GetDecimal()), Is.EqualTo(530m));
        Assert.That(movimientos.Select(m => m.GetProperty("numeroVersion").GetInt32()).Distinct(), Is.EquivalentTo(new[] { 1, 2, 3 }));
        Assert.That(movimientos.Select(m => m.GetProperty("estadoPresupuesto").GetString()).Distinct(),
            Is.EquivalentTo(new[] { "HISTORICO", "APROBADO" }));
        long[] ids = movimientos.Select(m => m.GetProperty("idMovimientoPresupuestal").GetInt64()).ToArray();
        Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        Assert.That(ids, Is.Ordered, "Todas las fechas del fixture son iguales; se desempata por ID.");
        Assert.That(movimientos.All(m => m.GetProperty("monto").GetDecimal() > 0), Is.True);
        Assert.That(movimientos.All(m => m.GetProperty("idPresupuesto").GetInt32() == _presupuesto
            && m.GetProperty("idCatalogoPartida").GetInt32() == _partida), Is.True);
        Assert.That(movimientos.Single(m => m.GetProperty("tipo").GetString() == "LIBERACION")
            .GetProperty("efectoComprometido").GetDecimal(), Is.EqualTo(-100m));

        JsonElement individual = await Json($"{Base}/presupuestos/{_presupuesto}/versiones/{_versionActual}/partidas/{_detalleActual}/movimientos");
        Assert.That(individual.GetArrayLength(), Is.EqualTo(2));
        Assert.That(individual[0].TryGetProperty("efectoEjecutado", out _), Is.False, "El contrato individual no cambia.");
        JsonElement historico = await Json($"{Base}/presupuestos/{_presupuesto}/versiones/{_versionHistorica}/partidas/{_detalleHistorico}/movimientos");
        Assert.That(historico.GetArrayLength(), Is.EqualTo(3));
    }

    [Test]
    public async Task SinConsolidado_ConservaVersionEstadoYConsumidorRequerimientos()
    {
        foreach (string modo in new[] { "", "&consolidado=false", "&estadoPresupuesto=APROBADO", $"&idPresupuestoVersion={_versionActual}" })
        {
            JsonElement saldo = Fila(await Obtener("saldo", modo, consolidado: false), _partida);
            AssertMontos(saldo, 1200m, 50m, 200m, 950m);
        }
        JsonElement historico = Fila(await Obtener("saldo", $"&idPresupuestoVersion={_versionHistorica}", false), _partida);
        AssertMontos(historico, 1000m, 300m, 300m, 400m);
        foreach (string reporte in Reportes.Where(r => r != "resumen" && r != "saldo"))
        {
            JsonElement fila = (await Obtener(reporte, consolidado: false)).EnumerateArray().First();
            Assert.That(fila.GetProperty("montoPresupuestado").GetDecimal(), Is.EqualTo(1200m), reporte);
            string magnitud = reporte == "presupuesto-vs-comprometido" ? "comprometido" : "ejecutado";
            Assert.That(fila.GetProperty(magnitud).GetDecimal(), Is.EqualTo(magnitud == "comprometido" ? 50m : 200m), reporte);
        }
        AssertMontos((await Obtener("resumen", consolidado: false)).GetProperty("totales"), 1200m, 50m, 200m, 950m, "presupuestado");
    }

    [Test]
    public async Task UnaAprobadaSinHistoricos_EquivaleALecturaIndividualYSinCruceEntrePresupuestos()
    {
        int presupuesto = await CrearPresupuesto("P-UNICO", _moneda);
        int version = await CrearVersion(presupuesto, 1, "APROBADO");
        int detalle = await CrearDetalle(version, _partida, 500m);
        await CrearDetalle(version, _partidaCero, 0m);
        await Movimiento(detalle, "COMPROMISO", 25m, "COMPROMISO", "INCREMENTO");
        await Movimiento(detalle, "EJECUCION", 75m, "EJECUCION", "INCREMENTO");
        foreach (string reporte in Reportes)
        {
            JsonElement individual = await Json($"{Base}/consultas/{reporte}?idPresupuesto={presupuesto}");
            JsonElement consolidado = await Json($"{Base}/consultas/{reporte}?idPresupuesto={presupuesto}&consolidado=true");
            AssertJsonEquivalente(individual, consolidado, reporte);
        }
        Assert.That((await Json(RutaMovimientos(presupuesto, _partidaCero))).GetArrayLength(), Is.Zero);
        Assert.That(Fila(await Obtener("saldo"), _partida).GetProperty("ejecutado").GetDecimal(), Is.EqualTo(530m));
    }

    [Test]
    public async Task SobreEjecucion_RecalculaSaldoExcesoYPorcentajeMayorACien()
    {
        await DbHelpers.QueryScalarAsync<int>(
            "UPDATE ControlPresupuestario.PresupuestoDetalle SET MontoPresupuestado = 400 WHERE IdPresupuestoDetalle = @Id; SELECT 1",
            new { Id = _detalleActual });
        JsonElement saldo = Fila(await Obtener("saldo"), _partida);
        AssertMontos(saldo, 400m, 375m, 530m, -505m);
        Assert.That(saldo.GetProperty("montoExcedido").GetDecimal(), Is.EqualTo(505m));
        Assert.That(saldo.GetProperty("excedido").GetBoolean(), Is.True);
        JsonElement ejecutado = Fila(await Obtener("presupuesto-vs-ejecutado"), _partida);
        Assert.That(ejecutado.GetProperty("porcentajeEjecutado").GetDecimal(), Is.EqualTo(132.5m));
        Assert.That(ejecutado.GetProperty("diferencia").GetDecimal(), Is.EqualTo(-130m));
        JsonElement totales = (await Obtener("resumen")).GetProperty("totales");
        Assert.That(totales.GetProperty("porcentajeEjecutado").GetDecimal(), Is.EqualTo(138.75m));
        Assert.That(totales.GetProperty("partidasExcedidas").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public async Task Historial_OrdenaPorFechaAntesDeIdYRechazaPresupuestoSinAprobada()
    {
        await DbHelpers.QueryScalarAsync<int>("""
            UPDATE ControlPresupuestario.MovimientoPresupuestal SET Fecha = '2026-02-01'
            WHERE IdMovimientoPresupuestal = (SELECT MIN(IdMovimientoPresupuestal)
                FROM ControlPresupuestario.MovimientoPresupuestal WHERE IdPresupuestoDetalle = @Detalle);
            SELECT 1;
            """, new { Detalle = _detalleHistorico });
        JsonElement[] movimientos = (await Json(RutaMovimientos(_presupuesto, _partida))).EnumerateArray().ToArray();
        Assert.That(movimientos.Select(m => m.GetProperty("fecha").GetDateTime()), Is.Ordered);
        Assert.That(movimientos.Last().GetProperty("monto").GetDecimal(), Is.EqualTo(400m));
        await DbHelpers.QueryScalarAsync<int>("""
            UPDATE ControlPresupuestario.PresupuestoVersion
            SET IdEstadoPresupuesto = (SELECT IdEstadoPresupuesto FROM ControlPresupuestario.EstadoPresupuesto WHERE Codigo = 'HISTORICO')
            WHERE IdPresupuestoVersion = @Version;
            SELECT 1;
            """, new { Version = _versionActual });
        Assert.That((await Obtener("saldo")).GetArrayLength(), Is.Zero);
        await Error(RutaMovimientos(_presupuesto, _partida), HttpStatusCode.NotFound, "PARTIDA_CONSOLIDADA_NO_ENCONTRADA");
    }

    [Test]
    public async Task FiltrosIncompatibles_SeRechazanEnLosSeisReportes()
    {
        foreach (string reporte in Reportes)
        foreach (string filtro in new[] { $"idPresupuestoVersion={_versionActual}", "estadoPresupuesto=APROBADO", "estadoPresupuesto=HISTORICO" })
            await Error($"{Base}/consultas/{reporte}?consolidado=true&{filtro}", HttpStatusCode.BadRequest, "FILTROS_INCOMPATIBLES");
    }

    [Test]
    public async Task MonedasMixtas_InactivosYFiltros_RespetanAlcance()
    {
        int otraMoneda = await DbHelpers.QueryScalarAsync<int>("SELECT TOP (1) IdMoneda FROM maestra.Moneda WHERE IdMoneda <> @Id", new { Id = _moneda });
        Assert.That(otraMoneda, Is.Positive, "El seed de monedas incluye PEN y USD.");
        int otroPresupuesto = await CrearPresupuesto("P-USD", otraMoneda);
        int otraVersion = await CrearVersion(otroPresupuesto, 1, "APROBADO");
        await CrearDetalle(otraVersion, _partida, 200m);
        foreach (string reporte in Reportes)
        {
            await Error($"{Base}/consultas/{reporte}?idCentroCosto={_centro}&consolidado=true", HttpStatusCode.BadRequest, "MONEDAS_MIXTAS");
            await Error($"{Base}/consultas/{reporte}?consolidado=true", HttpStatusCode.BadRequest, "MONEDAS_MIXTAS");
            JsonElement limitado = await Json($"{Base}/consultas/{reporte}?consolidado=true&idMoneda={_moneda}");
            Assert.That(reporte == "resumen" ? limitado.GetProperty("totales").GetProperty("presupuestado").GetDecimal()
                : limitado.EnumerateArray().Sum(f => f.GetProperty("montoPresupuestado").GetDecimal()), Is.EqualTo(1200m));
        }
        JsonElement partida = await Obtener("gastos-por-centro-costo", $"&idCatalogoPartida={_partida}");
        Assert.That(partida[0].GetProperty("ejecutado").GetDecimal(), Is.EqualTo(530m), "El modo consolidado filtra antes de agrupar.");
        Assert.That((await Obtener("saldo", "&idCentroCosto=2147483647")).GetArrayLength(), Is.Zero);
        await DbHelpers.QueryScalarAsync<int>("UPDATE ControlPresupuestario.Presupuesto SET Activo = 0 WHERE IdPresupuesto = @Id; SELECT 1", new { Id = _presupuesto });
        foreach (string reporte in Reportes)
        {
            JsonElement vacio = await Obtener(reporte);
            Assert.That(reporte == "resumen" ? vacio.GetProperty("totales").GetProperty("presupuestado").GetDecimal() : vacio.GetArrayLength(), Is.Zero);
            JsonElement incluido = await Obtener(reporte, "&incluirInactivos=true");
            Assert.That(reporte == "resumen" ? incluido.GetProperty("totales").GetProperty("presupuestado").GetDecimal()
                : incluido.EnumerateArray().Sum(f => f.GetProperty("montoPresupuestado").GetDecimal()), Is.EqualTo(1200m));
        }
        Assert.That((await Json(RutaMovimientos(_presupuesto, _partida))).GetArrayLength(), Is.EqualTo(11));
    }

    [Test]
    public async Task Historial_ValidaPertenenciaIdsYPermisos()
    {
        await Error(RutaMovimientos(int.MaxValue, _partida), HttpStatusCode.NotFound, "PRESUPUESTO_NO_ENCONTRADO");
        int ajena = await CrearPartida("3");
        await Error(RutaMovimientos(_presupuesto, ajena), HttpStatusCode.NotFound, "PARTIDA_CONSOLIDADA_NO_ENCONTRADA");
        foreach (string query in new[] { "", $"?idPresupuesto={_presupuesto}", "?idPresupuesto=0&idCatalogoPartida=-1" })
            Assert.That((await _client.GetAsync($"{Base}/consultas/movimientos{query}")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            JwtTestTokenFactory.CrearToken(permisos: new[] { CP.Reporte.Ver }));
        Assert.That((await _client.GetAsync(RutaMovimientos(_presupuesto, _partida))).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        _client.DefaultRequestHeaders.Authorization = null;
        Assert.That((await _client.GetAsync(RutaMovimientos(_presupuesto, _partida))).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task DashboardArbolYExclusiones_ConservanTodasSusMagnitudes()
    {
        string ruta = $"{Base}/consultas/dashboard?idCentroCosto={_centro}&idPresupuesto={_presupuesto}";
        JsonElement antes = await Json(ruta);
        foreach (string reporte in Reportes) await Obtener(reporte);
        JsonElement despues = await Json(ruta);
        Assert.That(despues.ToString(), Is.EqualTo(antes.ToString()));
        Assert.That(antes.GetProperty("resumen").GetProperty("ejecutado").GetDecimal(), Is.EqualTo(555m));
        JsonElement filtrado = await Json(ruta + $"&partidasExcluidas={_partida}");
        foreach (JsonProperty propiedad in antes.EnumerateObject().Where(p => p.Name != "rubrosFiltrados"))
            Assert.That(filtrado.GetProperty(propiedad.Name).ToString(), Is.EqualTo(propiedad.Value.ToString()), propiedad.Name);
        Assert.That(filtrado.GetProperty("rubrosFiltrados").EnumerateArray().Sum(r => r.GetProperty("ejecutado").GetDecimal()), Is.EqualTo(25m));
        JsonElement arbolVersion = await Json($"{Base}/presupuestos/{_presupuesto}/versiones/{_versionHistorica}/arbol");
        Assert.That(arbolVersion.GetProperty("totales").GetProperty("montoEjecutado").GetDecimal(), Is.EqualTo(325m));
    }

    private Task<JsonElement> Obtener(string reporte, string extra = "", bool consolidado = true)
        => Json($"{Base}/consultas/{reporte}?idPresupuesto={_presupuesto}{(consolidado ? "&consolidado=true" : "")}{extra}");

    private async Task<JsonElement> Json(string ruta)
    {
        using HttpResponseMessage respuesta = await _client.GetAsync(ruta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), await respuesta.Content.ReadAsStringAsync());
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task Error(string ruta, HttpStatusCode estado, string codigo)
    {
        using HttpResponseMessage respuesta = await _client.GetAsync(ruta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(estado), await respuesta.Content.ReadAsStringAsync());
        JsonElement error = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("error").GetString(), Is.EqualTo(codigo));
    }

    private static void AssertJsonEquivalente(JsonElement esperado, JsonElement actual, string contexto)
    {
        Assert.That(actual.ValueKind, Is.EqualTo(esperado.ValueKind), contexto);
        switch (esperado.ValueKind)
        {
            case JsonValueKind.Object:
                Assert.That(actual.EnumerateObject().Select(p => p.Name),
                    Is.EquivalentTo(esperado.EnumerateObject().Select(p => p.Name)), contexto);
                foreach (JsonProperty propiedad in esperado.EnumerateObject())
                    AssertJsonEquivalente(propiedad.Value, actual.GetProperty(propiedad.Name), contexto + "." + propiedad.Name);
                break;
            case JsonValueKind.Array:
                Assert.That(actual.GetArrayLength(), Is.EqualTo(esperado.GetArrayLength()), contexto);
                for (int i = 0; i < esperado.GetArrayLength(); i++)
                    AssertJsonEquivalente(esperado[i], actual[i], contexto + "[" + i + "]");
                break;
            case JsonValueKind.Number:
                Assert.That(actual.GetDecimal(), Is.EqualTo(esperado.GetDecimal()), contexto);
                break;
            default:
                Assert.That(actual.ToString(), Is.EqualTo(esperado.ToString()), contexto);
                break;
        }
    }

    private static JsonElement Fila(JsonElement filas, int partida)
        => filas.EnumerateArray().Single(f => f.GetProperty("idCatalogoPartida").GetInt32() == partida);

    private static string RutaMovimientos(int presupuesto, int partida)
        => $"{Base}/consultas/movimientos?idPresupuesto={presupuesto}&idCatalogoPartida={partida}";

    private static void AssertMontos(JsonElement fila, decimal presupuesto, decimal compromiso, decimal ejecucion, decimal saldo,
        string campoPresupuesto = "montoPresupuestado")
    {
        Assert.That(fila.GetProperty(campoPresupuesto).GetDecimal(), Is.EqualTo(presupuesto));
        Assert.That(fila.GetProperty("comprometido").GetDecimal(), Is.EqualTo(compromiso));
        Assert.That(fila.GetProperty("ejecutado").GetDecimal(), Is.EqualTo(ejecucion));
        Assert.That(fila.GetProperty("saldoDisponible").GetDecimal(), Is.EqualTo(saldo));
    }

    private static Task<int> CrearPartida(string codigo) => DbHelpers.QueryScalarAsync<int>("""
        INSERT INTO ControlPresupuestario.CatalogoPartida (Codigo, Nombre, IdTipoPartida, Nivel)
        SELECT @Codigo, 'Partida ' + @Codigo, IdTipoPartida, 1 FROM ControlPresupuestario.TipoPartida;
        SELECT CAST(SCOPE_IDENTITY() AS INT);
        """, new { Codigo = codigo });

    private Task<int> CrearPresupuesto(string codigo, int moneda) => DbHelpers.QueryScalarAsync<int>("""
        INSERT INTO ControlPresupuestario.Presupuesto (Codigo, Nombre, IdCentroCosto, IdMoneda)
        VALUES (@Codigo, 'Presupuesto', @Centro, @Moneda);
        SELECT CAST(SCOPE_IDENTITY() AS INT);
        """, new { Codigo = codigo, Centro = _centro, Moneda = moneda });

    private static Task<int> CrearVersion(int presupuesto, int numero, string estado) => DbHelpers.QueryScalarAsync<int>("""
        INSERT INTO ControlPresupuestario.PresupuestoVersion (IdPresupuesto, NumeroVersion, IdEstadoPresupuesto)
        SELECT @Presupuesto, @Numero, IdEstadoPresupuesto FROM ControlPresupuestario.EstadoPresupuesto WHERE Codigo = @Estado;
        SELECT CAST(SCOPE_IDENTITY() AS INT);
        """, new { Presupuesto = presupuesto, Numero = numero, Estado = estado });

    private static Task<int> CrearDetalle(int version, int partida, decimal monto) => DbHelpers.QueryScalarAsync<int>("""
        INSERT INTO ControlPresupuestario.PresupuestoDetalle (IdPresupuestoVersion, IdCatalogoPartida, MontoPresupuestado)
        VALUES (@Version, @Partida, @Monto);
        SELECT CAST(SCOPE_IDENTITY() AS INT);
        """, new { Version = version, Partida = partida, Monto = monto });

    private static Task<int> Movimiento(int detalle, string tipo, decimal monto, string afectacion, string direccion)
        => DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO ControlPresupuestario.MovimientoPresupuestal
                (IdPresupuestoDetalle, IdTipoMovimientoPresupuestal, ClaveEvento, Origen, IdOrigen, Afectacion, Direccion, Fecha, Monto)
            SELECT @Detalle, IdTipoMovimientoPresupuestal, @Clave, 'PRUEBA', 1, @Afectacion, @Direccion, '2026-01-01', @Monto
            FROM ControlPresupuestario.TipoMovimientoPresupuestal WHERE Codigo = @Tipo;
            SELECT 1;
            """, new { Detalle = detalle, Tipo = tipo, Monto = monto, Afectacion = afectacion, Direccion = direccion, Clave = Guid.NewGuid().ToString() });
}
