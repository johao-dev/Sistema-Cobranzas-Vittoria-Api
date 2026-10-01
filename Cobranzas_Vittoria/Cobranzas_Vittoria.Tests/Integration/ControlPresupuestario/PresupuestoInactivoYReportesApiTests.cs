using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Tests.Integration.Common;
using CP = Cobranzas_Vittoria.Seguridad.Authorization.Permisos.ControlPresupuestario;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

/// <summary>
/// Reportes por defecto sobre lo vigente (versiones APROBADAS de presupuestos activos) y presupuesto
/// inactivo: inactivarlo con registros pide confirmación y, ya inactivo, solo admite reversiones.
/// </summary>
[NonParallelizable]
public sealed class PresupuestoInactivoYReportesApiTests : IntegrationTestBase
{
    private const string Base = "/api/control-presupuestario";

    private static readonly string[] Permisos =
    {
        CP.CentroCosto.Crear, CP.Partida.Crear, CP.Presupuesto.Ver, CP.Presupuesto.Crear, CP.Presupuesto.Actualizar,
        CP.Presupuesto.EditarDetalle, CP.Presupuesto.RegistrarAjuste, CP.Version.Aprobar, CP.Reporte.Ver
    };

    private int _idPen;

    [SetUp]
    public async Task Preparar()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestTokenFactory.CrearToken(permisos: Permisos));
        _idPen = await DbHelpersMoneda.ObtenerPenAsync();
        await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO ControlPresupuestario.EstadoPresupuesto (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('BORRADOR'), ('APROBADO'), ('HISTORICO'), ('ANULADO')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.EstadoPresupuesto e WHERE e.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoMovimientoPresupuestal (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('COMPROMISO'), ('LIBERACION'), ('EJECUCION'), ('AJUSTE')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoMovimientoPresupuestal t WHERE t.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('ADMINISTRACION')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoCentroCosto t WHERE t.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('MATERIALES')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoPartida t WHERE t.Codigo = v.Codigo);
            SELECT 1;
            """);
    }

    [Test]
    public async Task Reportes_PorDefectoSoloLoVigente_ElBorradorSePideExplicitamente()
    {
        var cc = await CrearCentroCostoAsync();
        var partida = await CrearPartidaAsync();
        var (aprobado, vAprobada, _) = await CrearPresupuestoAsync(cc, "PRES-APR", partida, 1000m, aprobar: true);
        var (borrador, _, _) = await CrearPresupuestoAsync(cc, "PRES-BOR", partida, 500m, aprobar: false);

        foreach (var reporte in new[] { "saldo", "presupuesto-vs-comprometido", "presupuesto-vs-ejecutado", "gastos-por-partida", "gastos-por-centro-costo" })
        {
            var filas = await Filas($"{Base}/consultas/{reporte}?idCentroCosto={cc}");
            Assert.That(filas.Select(f => f.GetProperty("idPresupuesto").GetInt32()).Distinct(), Is.EqualTo(new[] { aprobado }), reporte);
        }
        var soloBorrador = await Filas($"{Base}/consultas/saldo?idCentroCosto={cc}&estadoPresupuesto=BORRADOR");
        Assert.That(soloBorrador.Select(f => f.GetProperty("idPresupuesto").GetInt32()).Distinct(), Is.EqualTo(new[] { borrador }));
        var porVersion = await Filas($"{Base}/consultas/saldo?idPresupuestoVersion={vAprobada}");
        Assert.That(porVersion, Has.Count.EqualTo(1));
        var resumen = await _client.GetFromJsonAsync<JsonElement>($"{Base}/consultas/resumen?idCentroCosto={cc}");
        Assert.That(resumen.GetProperty("totales").GetProperty("presupuestado").GetDecimal(), Is.EqualTo(1000m));
    }

    [Test]
    public async Task PresupuestoInactivo_PideConfirmacion_SaleDeLosReportesYSoloAdmiteReversiones()
    {
        var cc = await CrearCentroCostoAsync();
        var partida = await CrearPartidaAsync();
        var (idPresupuesto, idVersion, idDetalle) = await CrearPresupuestoAsync(cc, "PRES-INA", partida, 1000m, aprobar: true);
        var ajustes = $"{Base}/presupuestos/{idPresupuesto}/versiones/{idVersion}/partidas/{idDetalle}/ajustes";
        Assert.That((await Ajuste(ajustes, "INCREMENTO", 400m)).StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var sinConfirmar = await Inactivar(idPresupuesto, confirmar: false);
        Assert.That(sinConfirmar.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var error = await sinConfirmar.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("error").GetString(), Is.EqualTo("PRESUPUESTO_CON_REGISTROS"));
        Assert.That(error.GetProperty("message").GetString(), Does.Contain("registros asociados").And.Contain("1 movimiento"));

        var confirmado = await Inactivar(idPresupuesto, confirmar: true);
        Assert.That(confirmado.StatusCode, Is.EqualTo(HttpStatusCode.OK), await confirmado.Content.ReadAsStringAsync());

        Assert.That(await Filas($"{Base}/consultas/saldo?idCentroCosto={cc}"), Is.Empty, "Un presupuesto inactivo sale de los reportes.");
        Assert.That(await Filas($"{Base}/consultas/saldo?idCentroCosto={cc}&incluirInactivos=true"), Has.Count.EqualTo(1));

        var reversion = await Ajuste(ajustes, "DECREMENTO", 100m);
        Assert.That(reversion.StatusCode, Is.EqualTo(HttpStatusCode.Created), await reversion.Content.ReadAsStringAsync());
        var consumo = await Ajuste(ajustes, "INCREMENTO", 50m);
        Assert.That(consumo.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await consumo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString(), Is.EqualTo("RECURSO_INACTIVO"));
    }

    [Test]
    public async Task PresupuestoSinRegistros_SeInactivaSinConfirmar()
    {
        var cc = await CrearCentroCostoAsync();
        var partida = await CrearPartidaAsync();
        var (idPresupuesto, _, _) = await CrearPresupuestoAsync(cc, "PRES-VACIO", partida, 100m, aprobar: false);

        Assert.That((await Inactivar(idPresupuesto, confirmar: false)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // ---------------------------------------------------------------- apoyo

    private Task<HttpResponseMessage> Ajuste(string ruta, string direccion, decimal monto)
        => _client.PostAsJsonAsync(ruta, new { afectacion = "EJECUCION", direccion, monto, observacion = "Prueba " + direccion });

    private Task<HttpResponseMessage> Inactivar(int idPresupuesto, bool confirmar)
        => _client.PutAsJsonAsync($"{Base}/presupuestos/{idPresupuesto}",
            new { nombre = "Presupuesto inactivo", activo = false, confirmarInactivacion = confirmar });

    private async Task<List<JsonElement>> Filas(string ruta)
    {
        var r = await _client.GetAsync(ruta);
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK), await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
    }

    private async Task<int> CrearCentroCostoAsync()
    {
        var tipo = await DbHelpers.QueryScalarAsync<int>(
            "SELECT IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = 'ADMINISTRACION'");
        var r = await _client.PostAsJsonAsync($"{Base}/centros-costo", new { codigo = "CC-REP", nombre = "Centro reportes", idTipoCentroCosto = tipo });
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Created), await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idCentroCosto").GetInt32();
    }

    private async Task<int> CrearPartidaAsync()
    {
        var tipo = await DbHelpers.QueryScalarAsync<int>("SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES'");
        var r = await _client.PostAsJsonAsync($"{Base}/partidas", new { codigo = "REP-01", nombre = "Compra de terreno", idTipoPartida = tipo });
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.Created), await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idCatalogoPartida").GetInt32();
    }

    private async Task<(int IdPresupuesto, int IdVersion, int IdDetalle)> CrearPresupuestoAsync(int cc, string codigo, int partida,
        decimal monto, bool aprobar)
    {
        var p = await _client.PostAsJsonAsync($"{Base}/presupuestos",
            new { idCentroCosto = cc, idMoneda = _idPen, codigo, nombre = "Presupuesto " + codigo });
        Assert.That(p.StatusCode, Is.EqualTo(HttpStatusCode.Created), await p.Content.ReadAsStringAsync());
        var creado = await p.Content.ReadFromJsonAsync<JsonElement>();
        var (id, version) = (creado.GetProperty("idPresupuesto").GetInt32(), creado.GetProperty("idPresupuestoVersion").GetInt32());
        var detalle = await _client.PostAsJsonAsync($"{Base}/presupuestos/{id}/versiones/{version}/partidas",
            new { idCatalogoPartida = partida, montoPresupuestado = monto });
        Assert.That(detalle.StatusCode, Is.EqualTo(HttpStatusCode.Created), await detalle.Content.ReadAsStringAsync());
        var idDetalle = (await detalle.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idPresupuestoDetalle").GetInt32();
        if (aprobar)
            Assert.That((await _client.PostAsync($"{Base}/presupuestos/{id}/versiones/{version}/aprobar", null)).StatusCode,
                Is.EqualTo(HttpStatusCode.OK));
        return (id, version, idDetalle);
    }
}
