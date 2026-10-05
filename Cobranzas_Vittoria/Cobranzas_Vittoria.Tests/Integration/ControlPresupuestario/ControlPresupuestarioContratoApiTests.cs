using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.Tests.Integration.Common;
using CP = Cobranzas_Vittoria.Seguridad.Authorization.Permisos.ControlPresupuestario;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

/// <summary>
/// Contrato HTTP del módulo Control Presupuestario: rutas, códigos de estado, permisos por
/// capacidad de negocio y traducción de los rechazos SQL (400/404/409). Respawn vacía los
/// catálogos del módulo entre tests, así que cada test los recrea.
/// </summary>
[NonParallelizable]
public sealed class ControlPresupuestarioContratoApiTests : IntegrationTestBase
{
    private const string Base = "/api/control-presupuestario";

    private static readonly string[] TodosLosPermisos =
    {
        CP.CentroCosto.Ver, CP.CentroCosto.Crear, CP.CentroCosto.Actualizar,
        CP.Partida.Ver, CP.Partida.Crear, CP.Partida.Actualizar,
        CP.Presupuesto.Ver, CP.Presupuesto.Crear, CP.Presupuesto.Actualizar, CP.Presupuesto.EditarDetalle,
        CP.Presupuesto.RegistrarAjuste, CP.Version.Crear, CP.Version.Aprobar, CP.Version.Anular,
        CP.Movimiento.Ver, CP.Reporte.Ver
    };

    private int _idPen;

    [SetUp]
    public async Task Preparar()
    {
        UsarToken(TodosLosPermisos);
        _idPen = await DbHelpersMoneda.ObtenerPenAsync();
        await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO ControlPresupuestario.EstadoPresupuesto (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('BORRADOR'), ('APROBADO'), ('HISTORICO'), ('ANULADO')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.EstadoPresupuesto e WHERE e.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoMovimientoPresupuestal (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('COMPROMISO'), ('LIBERACION'), ('EJECUCION'), ('AJUSTE')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoMovimientoPresupuestal t WHERE t.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('PROYECTO'), ('ADMINISTRACION')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoCentroCosto t WHERE t.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('MATERIALES'), ('INDIRECTOS')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoPartida t WHERE t.Codigo = v.Codigo);
            SELECT 1;
            """);
    }

    private void UsarToken(IEnumerable<string>? permisos) =>
        _client.DefaultRequestHeaders.Authorization = permisos is null
            ? null
            : new AuthenticationHeaderValue("Bearer", JwtTestTokenFactory.CrearToken(permisos: permisos));

    // ---------------------------------------------------------------- centros de costo

    [Test]
    public async Task CentroCosto_CrearDevuelve201ConElRecursoYActualizarNoPermiteCambiarElCodigo()
    {
        var tipo = await IdTipoCentroCosto("ADMINISTRACION");
        var crear = await _client.PostAsJsonAsync($"{Base}/centros-costo",
            new { codigo = "ADMIN", nombre = "Administración", idTipoCentroCosto = tipo, idProyecto = (int?)null });

        Assert.That(crear.StatusCode, Is.EqualTo(HttpStatusCode.Created), await crear.Content.ReadAsStringAsync());
        var creado = await Json(crear);
        Assert.That(crear.Headers.Location?.ToString(), Does.EndWith("/centros-costo/" + Id(creado, "idCentroCosto")));
        Assert.That(creado.GetProperty("codigo").GetString(), Is.EqualTo("ADMIN"));
        Assert.That(creado.GetProperty("activo").GetBoolean(), Is.True);

        var id = Id(creado, "idCentroCosto");
        var cambiaCodigo = await _client.PutAsJsonAsync($"{Base}/centros-costo/{id}",
            new { codigo = "OTRO", nombre = "Administración", idTipoCentroCosto = tipo, activo = true });
        Assert.That(cambiaCodigo.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var actualizar = await _client.PutAsJsonAsync($"{Base}/centros-costo/{id}",
            new { codigo = "ADMIN", nombre = "Administración central", idTipoCentroCosto = tipo, idProyecto = (int?)null, activo = true });
        Assert.That(actualizar.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Json(actualizar)).GetProperty("nombre").GetString(), Is.EqualTo("Administración central"));
    }

    [Test]
    public async Task CentroCosto_InexistenteDevuelve404YCodigoDuplicado409()
    {
        Assert.That((await _client.GetAsync($"{Base}/centros-costo/999999")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var tipo = await IdTipoCentroCosto("ADMINISTRACION");
        await _client.PostAsJsonAsync($"{Base}/centros-costo", new { codigo = "DUP", nombre = "Uno", idTipoCentroCosto = tipo });
        var duplicado = await _client.PostAsJsonAsync($"{Base}/centros-costo", new { codigo = "DUP", nombre = "Dos", idTipoCentroCosto = tipo });

        Assert.That(duplicado.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await Json(duplicado)).GetProperty("error").GetString(), Is.EqualTo("CODIGO_DUPLICADO"));
    }

    [Test]
    public async Task Permisos_SinSesion401_SinCapacidad403()
    {
        UsarToken(null);
        Assert.That((await _client.GetAsync($"{Base}/centros-costo")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await _client.GetAsync($"{Base}/catalogos/monedas")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        UsarToken(new[] { CP.CentroCosto.Ver });
        Assert.That((await _client.GetAsync($"{Base}/centros-costo")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _client.PostAsJsonAsync($"{Base}/centros-costo",
            new { codigo = "X", nombre = "X", idTipoCentroCosto = 1 })).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await _client.GetAsync($"{Base}/presupuestos")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await _client.GetAsync($"{Base}/consultas/saldo")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Catalogos_SoloExigenSesion()
    {
        UsarToken(Array.Empty<string>());
        foreach (var catalogo in new[] { "estados-presupuesto", "tipos-centro-costo", "tipos-partida", "tipos-movimiento", "monedas", "secciones-gasto" })
        {
            var r = await _client.GetAsync($"{Base}/catalogos/{catalogo}");
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK), catalogo);
        }
        var estados = await _client.GetFromJsonAsync<JsonElement>($"{Base}/catalogos/estados-presupuesto");
        Assert.That(estados.EnumerateArray().Select(e => e.GetProperty("codigo").GetString()), Does.Contain("APROBADO"));
    }

    // ---------------------------------------------------------------- partidas

    [Test]
    public async Task Partida_ListaIncluyeElPadreYFiltroInvalidoDevuelve400()
    {
        var (padre, hoja) = await CrearPartidasAsync();
        var partidas = await _client.GetFromJsonAsync<JsonElement>($"{Base}/partidas");
        var hija = partidas.EnumerateArray().Single(p => Id(p, "idCatalogoPartida") == hoja);
        Assert.That(hija.GetProperty("idPartidaPadre").GetInt32(), Is.EqualTo(padre));

        Assert.That((await _client.GetAsync($"{Base}/partidas?soloRaices=true&idPartidaPadre={padre}")).StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // ---------------------------------------------------------------- presupuesto y versiones

    [Test]
    public async Task Presupuesto_CrearDevuelveLaVersion1EnBorrador()
    {
        var cc = await CrearCentroCostoAsync();
        var crear = await _client.PostAsJsonAsync($"{Base}/presupuestos", new
        {
            idCentroCosto = cc, idMoneda = _idPen, codigo = "PRES-T702-2026", nombre = "Presupuesto Talara 702",
            descripcion = "Presupuesto general del proyecto", fechaInicio = "2026-01-01", fechaFin = "2027-12-31"
        });

        Assert.That(crear.StatusCode, Is.EqualTo(HttpStatusCode.Created), await crear.Content.ReadAsStringAsync());
        var creado = await Json(crear);
        Assert.That(creado.GetProperty("numeroVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(creado.GetProperty("estado").GetString(), Is.EqualTo("BORRADOR"));
        Assert.That(Id(creado, "idPresupuestoVersion"), Is.Positive);

        var lista = await _client.GetFromJsonAsync<JsonElement>($"{Base}/presupuestos?idCentroCosto={cc}");
        var fila = lista.EnumerateArray().Single();
        Assert.That(fila.GetProperty("moneda").GetString(), Is.EqualTo("PEN"));
        Assert.That(fila.GetProperty("centroCosto").GetString(), Is.Not.Empty);
    }

    [Test]
    public async Task Version_FlujoCompleto_PartidasAprobarNuevaVersionYAnularConMotivo()
    {
        var (idPresupuesto, v1) = await CrearPresupuestoAsync();
        var (_, hoja) = await CrearPartidasAsync();

        var agregar = await _client.PostAsJsonAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/partidas",
            new { idCatalogoPartida = hoja, montoPresupuestado = 120000.00m });
        Assert.That(agregar.StatusCode, Is.EqualTo(HttpStatusCode.Created), await agregar.Content.ReadAsStringAsync());
        var detalle = await Json(agregar);
        Assert.That(detalle.GetProperty("codigo").GetString(), Is.EqualTo("EST-CON"));
        var idDetalle = Id(detalle, "idPresupuestoDetalle");

        var actualizar = await _client.PutAsJsonAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/partidas/{idDetalle}",
            new { montoPresupuestado = 135000.00m });
        Assert.That((await Json(actualizar)).GetProperty("montoPresupuestado").GetDecimal(), Is.EqualTo(135000.00m));

        var aprobar = await _client.PostAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/aprobar", null);
        Assert.That(aprobar.StatusCode, Is.EqualTo(HttpStatusCode.OK), await aprobar.Content.ReadAsStringAsync());
        Assert.That((await Json(aprobar)).GetProperty("estado").GetString(), Is.EqualTo("APROBADO"));

        // Una versión aprobada no admite cambios de montos ni anulación (SQL → 409).
        var editarAprobada = await _client.PutAsJsonAsync(
            $"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/partidas/{idDetalle}", new { montoPresupuestado = 1m });
        Assert.That(editarAprobada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var anularAprobada = await _client.PostAsJsonAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/anular",
            new { motivo = "No aplica" });
        Assert.That(anularAprobada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        var nueva = await _client.PostAsJsonAsync($"{Base}/presupuestos/{idPresupuesto}/versiones", new { });
        Assert.That(nueva.StatusCode, Is.EqualTo(HttpStatusCode.Created), await nueva.Content.ReadAsStringAsync());
        var v2 = await Json(nueva);
        Assert.That(v2.GetProperty("numeroVersion").GetInt32(), Is.EqualTo(2));
        Assert.That(v2.GetProperty("estado").GetString(), Is.EqualTo("BORRADOR"));
        var idV2 = Id(v2, "idPresupuestoVersion");

        var copiadas = await _client.GetFromJsonAsync<JsonElement>($"{Base}/presupuestos/{idPresupuesto}/versiones/{idV2}/partidas");
        Assert.That(copiadas.GetArrayLength(), Is.EqualTo(1), "La versión nueva copia el snapshot aprobado.");

        var sinMotivo = await _client.PostAsJsonAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{idV2}/anular", new { });
        Assert.That(sinMotivo.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var anular = await _client.PostAsJsonAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{idV2}/anular",
            new { motivo = "Se descartó esta propuesta presupuestaria" });
        Assert.That(anular.StatusCode, Is.EqualTo(HttpStatusCode.OK), await anular.Content.ReadAsStringAsync());
        var anulada = await Json(anular);
        Assert.That(anulada.GetProperty("estado").GetString(), Is.EqualTo("ANULADO"));
        Assert.That(anulada.GetProperty("motivoAnulacion").GetString(), Is.EqualTo("Se descartó esta propuesta presupuestaria"));
        Assert.That(anulada.GetProperty("usuarioAnulacion").GetString(), Is.Not.Empty);

        var versiones = await _client.GetFromJsonAsync<JsonElement>($"{Base}/presupuestos/{idPresupuesto}/versiones");
        Assert.That(versiones.EnumerateArray().Select(v => v.GetProperty("estado").GetString()),
            Is.EquivalentTo(new[] { "APROBADO", "ANULADO" }));
    }

    [Test]
    public async Task Version_DeOtroPresupuesto_Devuelve404()
    {
        var (idA, versionA) = await CrearPresupuestoAsync("PRES-A");
        var (idB, _) = await CrearPresupuestoAsync("PRES-B");

        Assert.That((await _client.GetAsync($"{Base}/presupuestos/{idA}/versiones/{versionA}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var cruzada = await _client.GetAsync($"{Base}/presupuestos/{idB}/versiones/{versionA}");
        Assert.That(cruzada.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await Json(cruzada)).GetProperty("error").GetString(), Is.EqualTo("VERSION_NO_ENCONTRADA"));
        Assert.That((await _client.GetAsync($"{Base}/presupuestos/999999")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Partida_EliminarDevuelve204YLaCargaCompletaReemplazaMontos()
    {
        var (idPresupuesto, v1) = await CrearPresupuestoAsync();
        var (_, hoja) = await CrearPartidasAsync();
        var otra = await CrearPartidaAsync("EST-ACE", "Acero", padre: await IdPartida("EST"));

        var lote = await _client.PutAsJsonAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/partidas/lote", new
        {
            detalles = new[] { new { idCatalogoPartida = hoja, montoPresupuestado = 1000m }, new { idCatalogoPartida = otra, montoPresupuestado = 500m } },
            quitarAusentes = false
        });
        Assert.That(lote.StatusCode, Is.EqualTo(HttpStatusCode.OK), await lote.Content.ReadAsStringAsync());
        Assert.That((await Json(lote)).GetProperty("montoTotal").GetDecimal(), Is.EqualTo(1500m));

        var partidas = await _client.GetFromJsonAsync<JsonElement>($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/partidas");
        var idDetalle = Id(partidas.EnumerateArray().First(), "idPresupuestoDetalle");
        var eliminar = await _client.DeleteAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/partidas/{idDetalle}");
        Assert.That(eliminar.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await _client.DeleteAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/partidas/{idDetalle}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ---------------------------------------------------------------- ledger y consultas

    [Test]
    public async Task Ajuste_ExcesoDeSaldo409_AjusteValidoSeLeeEnElLedgerYEnLasConsultas()
    {
        var (idPresupuesto, v1, idDetalle, cc) = await CrearPresupuestoAprobadoAsync(monto: 1000m);
        var ruta = $"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/partidas/{idDetalle}";

        var exceso = await _client.PostAsJsonAsync($"{ruta}/ajustes",
            new { afectacion = "EJECUCION", direccion = "INCREMENTO", monto = 1500m, observacion = "Factura sin registrar" });
        Assert.That(exceso.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await Json(exceso)).GetProperty("error").GetString(), Is.EqualTo("SALDO_INSUFICIENTE"));

        var ajuste = await _client.PostAsJsonAsync($"{ruta}/ajustes",
            new { afectacion = "EJECUCION", direccion = "INCREMENTO", monto = 400m, observacion = "Factura de enero sin registrar" });
        Assert.That(ajuste.StatusCode, Is.EqualTo(HttpStatusCode.Created), await ajuste.Content.ReadAsStringAsync());
        var movimiento = await Json(ajuste);
        Assert.That(movimiento.GetProperty("tipo").GetString(), Is.EqualTo("AJUSTE"));

        var ledger = await _client.GetFromJsonAsync<JsonElement>($"{ruta}/movimientos");
        Assert.That(ledger.GetArrayLength(), Is.EqualTo(1));
        var idMovimiento = movimiento.GetProperty("idMovimientoPresupuestal").GetInt64();
        Assert.That((await _client.GetAsync($"{Base}/movimientos/{idMovimiento}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _client.GetAsync($"{Base}/movimientos/999999999")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var saldo = await _client.GetFromJsonAsync<JsonElement>($"{Base}/consultas/saldo?idPresupuesto={idPresupuesto}");
        var fila = saldo.EnumerateArray().Single();
        Assert.That(fila.GetProperty("partida").GetString(), Is.EqualTo("Concreto"));
        Assert.That(fila.GetProperty("ejecutado").GetDecimal(), Is.EqualTo(400m));
        Assert.That(fila.GetProperty("comprometido").GetDecimal(), Is.EqualTo(0m));
        Assert.That(fila.GetProperty("saldoDisponible").GetDecimal(), Is.EqualTo(600m));

        var vigente = await _client.GetFromJsonAsync<JsonElement>($"{Base}/consultas/vigente?idCentroCosto={cc}");
        Assert.That(vigente.EnumerateArray().Single().GetProperty("saldoDisponible").GetDecimal(), Is.EqualTo(600m));

        var resumen = await _client.GetFromJsonAsync<JsonElement>($"{Base}/consultas/resumen?idCentroCosto={cc}");
        Assert.That(resumen.GetProperty("totales").GetProperty("ejecutado").GetDecimal(), Is.EqualTo(400m));
        Assert.That(resumen.GetProperty("totales").GetProperty("porcentajeEjecutado").GetDecimal(), Is.EqualTo(40m));
        Assert.That(resumen.GetProperty("partidasExcedidas").GetArrayLength(), Is.Zero);

        foreach (var consulta in new[] { "presupuesto-vs-comprometido", "presupuesto-vs-ejecutado", "gastos-por-partida", "gastos-por-centro-costo" })
            Assert.That((await _client.GetAsync($"{Base}/consultas/{consulta}?idCentroCosto={cc}")).StatusCode,
                Is.EqualTo(HttpStatusCode.OK), consulta);
        Assert.That((await _client.GetAsync($"{Base}/consultas/saldo?estadoPresupuesto=RARO")).StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Dashboard_SeComponeDeLasVistasY404SiElCentroNoExiste()
    {
        var (idPresupuesto, v1, idDetalle, cc) = await CrearPresupuestoAprobadoAsync(monto: 2000m);
        await _client.PostAsJsonAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/partidas/{idDetalle}/ajustes",
            new { afectacion = "EJECUCION", direccion = "INCREMENTO", monto = 500m, observacion = "Gasto de prueba", fecha = "2026-02-15" });

        var dashboard = await _client.GetAsync($"{Base}/consultas/dashboard?idCentroCosto={cc}");
        Assert.That(dashboard.StatusCode, Is.EqualTo(HttpStatusCode.OK), await dashboard.Content.ReadAsStringAsync());
        var d = await Json(dashboard);
        Assert.That(d.GetProperty("resumen").GetProperty("presupuestado").GetDecimal(), Is.EqualTo(2000m));
        Assert.That(d.GetProperty("resumen").GetProperty("ejecutado").GetDecimal(), Is.EqualTo(500m));
        Assert.That(d.GetProperty("alCorte").GetProperty("realAcumulado").GetDecimal(), Is.EqualTo(500m));
        Assert.That(d.GetProperty("encabezado").GetProperty("codigoMoneda").GetString(), Is.EqualTo("PEN"));
        Assert.That(d.GetProperty("rubros").GetArrayLength(), Is.EqualTo(1));

        Assert.That((await _client.GetAsync($"{Base}/consultas/dashboard?idCentroCosto=999999")).StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ---------------------------------------------------------------- apoyo

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static int Id(JsonElement e, string propiedad) => e.GetProperty(propiedad).GetInt32();

    private static Task<int> IdTipoCentroCosto(string codigo) => DbHelpers.QueryScalarAsync<int>(
        "SELECT IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = @codigo", new { codigo });

    private static Task<int> IdPartida(string codigo) => DbHelpers.QueryScalarAsync<int>(
        "SELECT IdCatalogoPartida FROM ControlPresupuestario.CatalogoPartida WHERE Codigo = @codigo", new { codigo });

    private async Task<int> CrearCentroCostoAsync(string codigo = "CC-API")
    {
        var r = await _client.PostAsJsonAsync($"{Base}/centros-costo",
            new { codigo, nombre = "Centro " + codigo, idTipoCentroCosto = await IdTipoCentroCosto("ADMINISTRACION") });
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Created), await r.Content.ReadAsStringAsync());
        return Id(await Json(r), "idCentroCosto");
    }

    private async Task<int> CrearPartidaAsync(string codigo, string nombre, int? padre = null)
    {
        var tipo = await DbHelpers.QueryScalarAsync<int>(
            "SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES'");
        var r = await _client.PostAsJsonAsync($"{Base}/partidas", new { codigo, nombre, idTipoPartida = tipo, idPartidaPadre = padre });
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Created), await r.Content.ReadAsStringAsync());
        return Id(await Json(r), "idCatalogoPartida");
    }

    private async Task<(int Padre, int Hoja)> CrearPartidasAsync()
    {
        var padre = await CrearPartidaAsync("EST", "Estructuras");
        var hoja = await CrearPartidaAsync("EST-CON", "Concreto", padre);
        return (padre, hoja);
    }

    private async Task<(int IdPresupuesto, int IdVersion)> CrearPresupuestoAsync(string codigo = "PRES-API")
    {
        var cc = await CrearCentroCostoAsync("CC-" + codigo);
        var r = await _client.PostAsJsonAsync($"{Base}/presupuestos", new
        {
            idCentroCosto = cc, idMoneda = _idPen, codigo, nombre = "Presupuesto " + codigo,
            fechaInicio = "2026-01-01", fechaFin = "2026-12-31"
        });
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Created), await r.Content.ReadAsStringAsync());
        var creado = await Json(r);
        return (Id(creado, "idPresupuesto"), Id(creado, "idPresupuestoVersion"));
    }

    private async Task<(int IdPresupuesto, int IdVersion, int IdDetalle, int IdCentroCosto)> CrearPresupuestoAprobadoAsync(decimal monto)
    {
        var (idPresupuesto, v1) = await CrearPresupuestoAsync();
        var (_, hoja) = await CrearPartidasAsync();
        var agregar = await _client.PostAsJsonAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/partidas",
            new { idCatalogoPartida = hoja, montoPresupuestado = monto });
        var idDetalle = Id(await Json(agregar), "idPresupuestoDetalle");
        var aprobar = await _client.PostAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{v1}/aprobar", null);
        Assert.That(aprobar.StatusCode, Is.EqualTo(HttpStatusCode.OK), await aprobar.Content.ReadAsStringAsync());
        var cc = Id(await _client.GetFromJsonAsync<JsonElement>($"{Base}/presupuestos/{idPresupuesto}"), "idCentroCosto");
        return (idPresupuesto, v1, idDetalle, cc);
    }
}
