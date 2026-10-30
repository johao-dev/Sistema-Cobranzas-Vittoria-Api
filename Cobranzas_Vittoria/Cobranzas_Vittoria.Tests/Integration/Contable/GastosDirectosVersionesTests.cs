using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

public sealed partial class GastosDirectosControllerTests
{
    [TestCase(1)]
    [TestCase(3)]
    public async Task Confirmar_TrasCambiosDeVersion_ReasignaGastoYEjecucionAlVigente(int revisiones)
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        RevisionGastoPrueba vigente = null!;
        for (int indice = 0; indice < revisiones; indice++)
            vigente = await CrearRevisionGastoAsync(presupuesto);

        HttpResponseMessage respuesta = await _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/confirmar", null);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), await respuesta.Content.ReadAsStringAsync());
        GastoVersionPrueba gasto = await LeerGastoVersionAsync(idGasto);
        Assert.That(gasto.Estado, Is.EqualTo("CONFIRMADO"));
        Assert.That(gasto.IdPresupuestoDetalle, Is.EqualTo(vigente.IdPresupuestoDetalle));
        Assert.That(await ContarMovimientosAsync(idGasto, "EJECUCION"), Is.EqualTo(1));
        Assert.That(await DbHelpers.QueryScalarAsync<int>("""
            SELECT IdPresupuestoDetalle FROM ControlPresupuestario.MovimientoPresupuestal
            WHERE Origen = 'GASTO_DIRECTO' AND IdOrigen = @idGasto;
            """, new { idGasto }), Is.EqualTo(vigente.IdPresupuestoDetalle));

        await VerificarControlVigenteAsync(presupuesto, ejecutado: 100m, saldo: 900m);
        Assert.That(await DbHelpers.QueryScalarAsync<decimal>("""
            SELECT MontoEjecutado FROM ControlPresupuestario.vw_PresupuestoResumen
            WHERE IdPresupuestoDetalle = @detalle;
            """, new { detalle = presupuesto.IdDetalleOriginal }), Is.Zero);
    }

    [Test]
    public async Task Confirmar_SaldoVigenteInsuficiente_ConservaDetalleOriginalYSinMovimiento()
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idPrevio = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 200m);
        await ConfirmarGastoPorSpAsync(idPrevio);
        int idPendiente = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        await CrearRevisionGastoAsync(presupuesto, monto: 250m);

        HttpResponseMessage respuesta = await _client.PostAsync($"/api/contable/gastos-directos/{idPendiente}/confirmar", null);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        JsonElement error = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("error").GetString(), Is.EqualTo("SALDO_INSUFICIENTE"));
        GastoVersionPrueba gasto = await LeerGastoVersionAsync(idPendiente);
        Assert.That(gasto.Estado, Is.EqualTo("REGISTRADO"));
        Assert.That(gasto.IdPresupuestoDetalle, Is.EqualTo(presupuesto.IdDetalleOriginal));
        Assert.That(await ContarMovimientosAsync(idPendiente), Is.Zero);
        await VerificarControlVigenteAsync(presupuesto, ejecutado: 200m, saldo: 50m);
    }

    [TestCase("BORRADOR")]
    [TestCase("ANULADO")]
    public async Task Confirmar_OrigenNuncaAprobado_NoLoReasigna(string estado)
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        RevisionGastoPrueba revision = await CrearRevisionGastoAsync(presupuesto, aprobar: false);
        int idGasto = await CrearAsync(revision.IdPresupuestoDetalle, presupuesto.IdMoneda, 100m);
        if (estado == "ANULADO")
        {
            await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();
            await conexion.ExecuteAsync("ControlPresupuestario.usp_PresupuestoVersion_Anular",
                new { revision.IdPresupuestoVersion }, commandType: CommandType.StoredProcedure);
        }

        HttpResponseMessage respuesta = await _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/confirmar", null);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        JsonElement error = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("error").GetString(), Is.EqualTo("VERSION_NO_VIGENTE"));
        GastoVersionPrueba gasto = await LeerGastoVersionAsync(idGasto);
        Assert.That(gasto.IdPresupuestoDetalle, Is.EqualTo(revision.IdPresupuestoDetalle));
        Assert.That(gasto.Estado, Is.EqualTo("REGISTRADO"));
        Assert.That(await ContarMovimientosAsync(idGasto), Is.Zero);
    }

    [Test]
    public async Task Confirmar_PartidaSoloEnOtroPresupuesto_RechazaSinReasignar()
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        RevisionGastoPrueba vigente = await CrearRevisionGastoAsync(presupuesto);
        await CrearOtroPresupuestoConMismaPartidaAsync(presupuesto);
        // Simular una base preexistente inconsistente: la aprobación corregida
        // ya no permite omitir esta partida mientras exista el gasto pendiente.
        await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();
        await conexion.ExecuteAsync("""
            DELETE FROM ControlPresupuestario.PresupuestoDetalle
            WHERE IdPresupuestoDetalle = @IdPresupuestoDetalle;
            """, vigente);

        HttpResponseMessage respuesta = await _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/confirmar", null);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        GastoVersionPrueba gasto = await LeerGastoVersionAsync(idGasto);
        Assert.That(gasto.IdPresupuestoDetalle, Is.EqualTo(presupuesto.IdDetalleOriginal));
        Assert.That(gasto.Estado, Is.EqualTo("REGISTRADO"));
        Assert.That(await ContarMovimientosAsync(idGasto), Is.Zero);
    }

    [Test]
    public async Task Confirmar_DosVersionesAprobadas_RechazaSinElegirUna()
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        await CrearRevisionGastoAsync(presupuesto);
        // Simular una inconsistencia producida fuera del flujo de aprobación.
        await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();
        await conexion.ExecuteAsync("""
            UPDATE pv SET IdEstadoPresupuesto = ep.IdEstadoPresupuesto
            FROM ControlPresupuestario.PresupuestoVersion pv
            JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion = pv.IdPresupuestoVersion
            CROSS JOIN ControlPresupuestario.EstadoPresupuesto ep
            WHERE pd.IdPresupuestoDetalle = @detalle AND ep.Codigo = 'APROBADO';
            """, new { detalle = presupuesto.IdDetalleOriginal });

        HttpResponseMessage respuesta = await _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/confirmar", null);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        GastoVersionPrueba gasto = await LeerGastoVersionAsync(idGasto);
        Assert.That(gasto.IdPresupuestoDetalle, Is.EqualTo(presupuesto.IdDetalleOriginal));
        Assert.That(gasto.Estado, Is.EqualTo("REGISTRADO"));
        Assert.That(await ContarMovimientosAsync(idGasto), Is.Zero);
    }

    [Test]
    public async Task Confirmar_RetryTrasOtraVersion_ConservaMovimientoYDetalleDeConfirmacion()
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        RevisionGastoPrueba confirmada = await CrearRevisionGastoAsync(presupuesto);
        await ConfirmarGastoPorSpAsync(idGasto);
        long idMovimiento = await DbHelpers.QueryScalarAsync<long>("""
            SELECT IdMovimientoPresupuestal FROM ControlPresupuestario.MovimientoPresupuestal
            WHERE Origen = 'GASTO_DIRECTO' AND IdOrigen = @idGasto;
            """, new { idGasto });
        await CrearRevisionGastoAsync(presupuesto, monto: 0m);

        HttpResponseMessage respuesta = await _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/confirmar", null);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), await respuesta.Content.ReadAsStringAsync());
        Assert.That((await LeerGastoVersionAsync(idGasto)).IdPresupuestoDetalle,
            Is.EqualTo(confirmada.IdPresupuestoDetalle));
        Assert.That(await ContarMovimientosAsync(idGasto), Is.EqualTo(1));
        Assert.That(await DbHelpers.QueryScalarAsync<long>("""
            SELECT IdMovimientoPresupuestal FROM ControlPresupuestario.MovimientoPresupuestal
            WHERE Origen = 'GASTO_DIRECTO' AND IdOrigen = @idGasto;
            """, new { idGasto }), Is.EqualTo(idMovimiento));
        await VerificarControlVigenteAsync(presupuesto, ejecutado: 100m, saldo: -100m);
    }

    [Test]
    public async Task Anular_ReasignadoYConOtraVersion_RevierteUnaVezEnDetalleDeConfirmacion()
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        RevisionGastoPrueba confirmada = await CrearRevisionGastoAsync(presupuesto);
        await ConfirmarGastoPorSpAsync(idGasto);
        await CrearRevisionGastoAsync(presupuesto);

        for (int intento = 0; intento < 2; intento++)
        {
            HttpResponseMessage respuesta = await _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/anular", null);
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), await respuesta.Content.ReadAsStringAsync());
        }

        GastoVersionPrueba gasto = await LeerGastoVersionAsync(idGasto);
        Assert.That(gasto.Estado, Is.EqualTo("ANULADO"));
        Assert.That(gasto.IdPresupuestoDetalle, Is.EqualTo(confirmada.IdPresupuestoDetalle));
        AjusteRow ajuste = (await DbHelpers.QueryAsync<AjusteRow>("""
            SELECT mp.IdPresupuestoDetalle, mp.Afectacion, mp.Direccion
            FROM ControlPresupuestario.MovimientoPresupuestal mp
            JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
                ON tm.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
            WHERE mp.Origen = 'GASTO_DIRECTO' AND mp.IdOrigen = @idGasto AND tm.Codigo = 'AJUSTE';
            """, new { idGasto })).Single();
        Assert.That(ajuste.IdPresupuestoDetalle, Is.EqualTo(confirmada.IdPresupuestoDetalle));
        Assert.That(ajuste.Afectacion, Is.EqualTo("EJECUCION"));
        Assert.That(ajuste.Direccion, Is.EqualTo("DECREMENTO"));
        Assert.That(await ContarMovimientosAsync(idGasto, "EJECUCION"), Is.EqualTo(1));
        Assert.That(await ContarMovimientosAsync(idGasto, "AJUSTE"), Is.EqualTo(1));
        await VerificarControlVigenteAsync(presupuesto, ejecutado: 0m, saldo: 1_000m);
    }

    [Test]
    public async Task Confirmar_TransaccionExterna_RollbackRevierteReasignacionYEjecucion()
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        RevisionGastoPrueba vigente = await CrearRevisionGastoAsync(presupuesto);
        await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();
        using SqlTransaction transaccion = conexion.BeginTransaction();

        await conexion.ExecuteAsync("contable.usp_GastoDirecto_Confirmar", new { IdGastoDirecto = idGasto },
            transaccion, commandType: CommandType.StoredProcedure);
        Assert.That(await conexion.QuerySingleAsync<int>("""
            SELECT IdPresupuestoDetalle FROM contable.GastoDirecto WHERE IdGastoDirecto = @idGasto;
            """, new { idGasto }, transaccion), Is.EqualTo(vigente.IdPresupuestoDetalle));
        transaccion.Rollback();

        GastoVersionPrueba gasto = await LeerGastoVersionAsync(idGasto);
        Assert.That(gasto.Estado, Is.EqualTo("REGISTRADO"));
        Assert.That(gasto.IdPresupuestoDetalle, Is.EqualTo(presupuesto.IdDetalleOriginal));
        Assert.That(await ContarMovimientosAsync(idGasto), Is.Zero);
    }

    [TestCase(0)]
    [TestCase(2)]
    public async Task Aprobar_NoOmitePartidaConGastoPendiente_AunqueEsteEnHistorico(int revisiones)
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        for (int indice = 0; indice < revisiones; indice++)
            await CrearRevisionGastoAsync(presupuesto);
        RevisionGastoPrueba revision = await CrearRevisionGastoAsync(presupuesto, aprobar: false);
        await SustituirPartidaRevisionAsync(presupuesto, revision);
        await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();

        SqlException? error = Assert.ThrowsAsync<SqlException>(async () =>
            await AprobarRevisionGastoAsync(conexion, revision.IdPresupuestoVersion));

        Assert.That(error!.Number, Is.EqualTo(51214));
        Assert.That(error.Message, Does.Contain("REGISTRADO"));
        Assert.That(await conexion.QuerySingleAsync<string>("""
            SELECT ep.Codigo FROM ControlPresupuestario.PresupuestoVersion pv
            JOIN ControlPresupuestario.EstadoPresupuesto ep ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
            WHERE pv.IdPresupuestoVersion = @IdPresupuestoVersion;
            """, revision), Is.EqualTo("BORRADOR"));
        Assert.That(await conexion.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoVersion pv
            JOIN ControlPresupuestario.EstadoPresupuesto ep ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
            WHERE pv.IdPresupuesto = @IdPresupuesto AND ep.Codigo = 'APROBADO';
            """, presupuesto), Is.EqualTo(1));
        Assert.That(await ContarMovimientosAsync(idGasto), Is.Zero);

        // Un detalle real con cero conserva la partida sin autorizar consumo.
        await conexion.ExecuteAsync("ControlPresupuestario.usp_PresupuestoDetalle_Agregar",
            new { revision.IdPresupuestoVersion, IdCatalogoPartida = presupuesto.IdPartida, MontoPresupuestado = 0m },
            commandType: CommandType.StoredProcedure);
        await AprobarRevisionGastoAsync(conexion, revision.IdPresupuestoVersion);
        HttpResponseMessage respuesta = await _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/confirmar", null);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        JsonElement rechazo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(rechazo.GetProperty("error").GetString(), Is.EqualTo("SALDO_INSUFICIENTE"));
    }

    [Test]
    public async Task Aprobar_GastoRegistradoAnulado_PermiteOmitirSuPartida()
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        HttpResponseMessage anulacion = await _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/anular", null);
        Assert.That(anulacion.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        RevisionGastoPrueba revision = await CrearRevisionGastoAsync(presupuesto, aprobar: false);
        await SustituirPartidaRevisionAsync(presupuesto, revision);

        await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();
        await AprobarRevisionGastoAsync(conexion, revision.IdPresupuestoVersion);

        Assert.That(await ContarMovimientosAsync(idGasto), Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Concurrencia_AprobarYConfirmar_UsaVersionSegunOrdenSerial(bool aprobarPrimero)
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        RevisionGastoPrueba revision = await CrearRevisionGastoAsync(presupuesto, aprobar: false);
        await using SqlConnection primera = await DbHelpers.OpenTestConnectionAsync();
        await using SqlConnection segunda = await DbHelpers.OpenTestConnectionAsync();
        int idSesionPrimera = await primera.QuerySingleAsync<int>("SELECT @@SPID;");
        int idSesionSegunda = await segunda.QuerySingleAsync<int>("SELECT @@SPID;");
        using SqlTransaction transaccion = primera.BeginTransaction();

        if (aprobarPrimero)
            await AprobarRevisionGastoAsync(primera, revision.IdPresupuestoVersion, transaccion);
        else
            await ConfirmarGastoPorSpAsync(idGasto, primera, transaccion);

        Task concurrente = aprobarPrimero
            ? ConfirmarGastoPorSpAsync(idGasto, segunda)
            : AprobarRevisionGastoAsync(segunda, revision.IdPresupuestoVersion);
        bool esperoBloqueo = await EsperarBloqueoPresupuestarioAsync(idSesionSegunda, idSesionPrimera);
        transaccion.Commit();
        await concurrente.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.That(esperoBloqueo, Is.True, "La segunda operación debe esperar la transacción del presupuesto.");
        int detalleEsperado = aprobarPrimero ? revision.IdPresupuestoDetalle : presupuesto.IdDetalleOriginal;
        Assert.That((await LeerGastoVersionAsync(idGasto)).IdPresupuestoDetalle, Is.EqualTo(detalleEsperado));
        Assert.That(await DbHelpers.QueryScalarAsync<int>("""
            SELECT IdPresupuestoDetalle FROM ControlPresupuestario.MovimientoPresupuestal
            WHERE Origen = 'GASTO_DIRECTO' AND IdOrigen = @idGasto;
            """, new { idGasto }), Is.EqualTo(detalleEsperado));
        Assert.That(await ContarMovimientosAsync(idGasto), Is.EqualTo(1));
        await VerificarControlVigenteAsync(presupuesto, ejecutado: 100m, saldo: 900m);
    }

    [Test]
    public async Task Concurrencia_ConfirmarDosVecesTrasRevision_CreaUnaEjecucion()
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        RevisionGastoPrueba vigente = await CrearRevisionGastoAsync(presupuesto);

        HttpResponseMessage[] respuestas = await Task.WhenAll(
            _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/confirmar", null),
            _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/confirmar", null));

        Assert.That(respuestas.Select(respuesta => respuesta.StatusCode), Is.All.EqualTo(HttpStatusCode.OK));
        Assert.That((await LeerGastoVersionAsync(idGasto)).IdPresupuestoDetalle, Is.EqualTo(vigente.IdPresupuestoDetalle));
        Assert.That(await ContarMovimientosAsync(idGasto), Is.EqualTo(1));
        await VerificarControlVigenteAsync(presupuesto, ejecutado: 100m, saldo: 900m);
    }

    [Test]
    public async Task Concurrencia_DosGastosTrasRevision_NoConsumenElMismoSaldo()
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int primero = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 600m);
        int segundo = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 600m);
        RevisionGastoPrueba vigente = await CrearRevisionGastoAsync(presupuesto);

        HttpResponseMessage[] respuestas = await Task.WhenAll(
            _client.PostAsync($"/api/contable/gastos-directos/{primero}/confirmar", null),
            _client.PostAsync($"/api/contable/gastos-directos/{segundo}/confirmar", null));

        Assert.That(respuestas.Select(respuesta => respuesta.StatusCode),
            Is.EquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        int confirmado = respuestas[0].StatusCode == HttpStatusCode.OK ? primero : segundo;
        int pendiente = confirmado == primero ? segundo : primero;
        Assert.That((await LeerGastoVersionAsync(confirmado)).IdPresupuestoDetalle, Is.EqualTo(vigente.IdPresupuestoDetalle));
        GastoVersionPrueba gastoPendiente = await LeerGastoVersionAsync(pendiente);
        Assert.That(gastoPendiente.IdPresupuestoDetalle, Is.EqualTo(presupuesto.IdDetalleOriginal));
        Assert.That(gastoPendiente.Estado, Is.EqualTo("REGISTRADO"));
        Assert.That(await ContarMovimientosAsync(pendiente), Is.Zero);
        await VerificarControlVigenteAsync(presupuesto, ejecutado: 600m, saldo: 400m);
    }

    [Test]
    public async Task Concurrencia_ConfirmarYAnularTrasRevision_NoDejaEjecucionNeta()
    {
        PresupuestoGastoPrueba presupuesto = await PrepararPresupuestoGastoAsync();
        int idGasto = await CrearAsync(presupuesto.IdDetalleOriginal, presupuesto.IdMoneda, 100m);
        await CrearRevisionGastoAsync(presupuesto);

        HttpResponseMessage[] respuestas = await Task.WhenAll(
            _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/confirmar", null),
            _client.PostAsync($"/api/contable/gastos-directos/{idGasto}/anular", null));

        Assert.That(respuestas[0].StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.Conflict));
        Assert.That(respuestas[1].StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await LeerGastoVersionAsync(idGasto)).Estado, Is.EqualTo("ANULADO"));
        Assert.That(await ContarMovimientosAsync(idGasto, "AJUSTE"),
            Is.EqualTo(await ContarMovimientosAsync(idGasto, "EJECUCION")));
        await VerificarControlVigenteAsync(presupuesto, ejecutado: 0m, saldo: 1_000m);
    }

    private static async Task<PresupuestoGastoPrueba> PrepararPresupuestoGastoAsync()
    {
        int detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync(monto: 1_000m);
        await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();
        return await conexion.QuerySingleAsync<PresupuestoGastoPrueba>("""
            SELECT pv.IdPresupuesto, pd.IdPresupuestoDetalle AS IdDetalleOriginal,
                pd.IdCatalogoPartida AS IdPartida, p.IdMoneda
            FROM ControlPresupuestario.PresupuestoDetalle pd
            JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = pv.IdPresupuesto
            WHERE pd.IdPresupuestoDetalle = @detalle;
            """, new { detalle });
    }

    private static async Task<RevisionGastoPrueba> CrearRevisionGastoAsync(
        PresupuestoGastoPrueba presupuesto, decimal? monto = null, bool aprobar = true)
    {
        await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();
        RevisionGastoPrueba revision = await conexion.QuerySingleAsync<RevisionGastoPrueba>(
            "ControlPresupuestario.usp_PresupuestoVersion_CrearNueva",
            new { presupuesto.IdPresupuesto, UsuarioCreacion = "test-versiones-gasto" },
            commandType: CommandType.StoredProcedure);
        revision.IdPresupuestoDetalle = await conexion.QuerySingleAsync<int>("""
            SELECT IdPresupuestoDetalle FROM ControlPresupuestario.PresupuestoDetalle
            WHERE IdPresupuestoVersion = @version AND IdCatalogoPartida = @partida;
            """, new { version = revision.IdPresupuestoVersion, partida = presupuesto.IdPartida });

        if (monto.HasValue)
            await conexion.ExecuteAsync("ControlPresupuestario.usp_PresupuestoDetalle_Actualizar",
                new { revision.IdPresupuestoDetalle, MontoPresupuestado = monto.Value },
                commandType: CommandType.StoredProcedure);

        if (aprobar)
            await AprobarRevisionGastoAsync(conexion, revision.IdPresupuestoVersion);

        return revision;
    }

    private static Task AprobarRevisionGastoAsync(SqlConnection conexion, int version, SqlTransaction? transaccion = null)
        => conexion.ExecuteAsync("ControlPresupuestario.usp_PresupuestoVersion_Aprobar",
            new { IdPresupuestoVersion = version, UsuarioAprobacion = "test-versiones-gasto" },
            transaccion, commandType: CommandType.StoredProcedure, commandTimeout: 15);

    private static async Task CrearOtroPresupuestoConMismaPartidaAsync(PresupuestoGastoPrueba presupuesto)
    {
        await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();
        int centro = await conexion.QuerySingleAsync<int>("""
            SELECT IdCentroCosto FROM ControlPresupuestario.Presupuesto WHERE IdPresupuesto = @IdPresupuesto;
            """, presupuesto);
        RevisionGastoPrueba otro = await conexion.QuerySingleAsync<RevisionGastoPrueba>(
            "ControlPresupuestario.usp_Presupuesto_Crear",
            new { IdCentroCosto = centro, presupuesto.IdMoneda, Codigo = Guid.NewGuid().ToString("N"), Nombre = "Otro presupuesto" },
            commandType: CommandType.StoredProcedure);
        await conexion.ExecuteAsync("ControlPresupuestario.usp_PresupuestoDetalle_Agregar",
            new { otro.IdPresupuestoVersion, IdCatalogoPartida = presupuesto.IdPartida, MontoPresupuestado = 10_000m },
            commandType: CommandType.StoredProcedure);
        await AprobarRevisionGastoAsync(conexion, otro.IdPresupuestoVersion);
    }

    private static async Task ConfirmarGastoPorSpAsync(int idGasto, SqlConnection? conexion = null,
        SqlTransaction? transaccion = null)
    {
        if (conexion is null)
        {
            await using SqlConnection propia = await DbHelpers.OpenTestConnectionAsync();
            await ConfirmarGastoPorSpAsync(idGasto, propia);
            return;
        }

        await conexion.ExecuteAsync("contable.usp_GastoDirecto_Confirmar", new { IdGastoDirecto = idGasto },
            transaccion, commandType: CommandType.StoredProcedure, commandTimeout: 15);
    }

    private static async Task<GastoVersionPrueba> LeerGastoVersionAsync(int idGasto)
        => (await DbHelpers.QueryAsync<GastoVersionPrueba>("""
            SELECT IdPresupuestoDetalle, Estado FROM contable.GastoDirecto WHERE IdGastoDirecto = @idGasto;
            """, new { idGasto })).Single();

    private static async Task VerificarControlVigenteAsync(PresupuestoGastoPrueba presupuesto, decimal ejecutado, decimal saldo)
    {
        ControlVigenteGastoPrueba control = (await DbHelpers.QueryAsync<ControlVigenteGastoPrueba>("""
            SELECT MontoEjecutado, SaldoDisponible FROM ControlPresupuestario.vw_ControlPresupuestarioVigente
            WHERE IdPresupuesto = @IdPresupuesto AND IdCatalogoPartida = @IdPartida;
            """, presupuesto)).Single();
        Assert.That(control.MontoEjecutado, Is.EqualTo(ejecutado));
        Assert.That(control.SaldoDisponible, Is.EqualTo(saldo));
    }

    private static async Task SustituirPartidaRevisionAsync(PresupuestoGastoPrueba presupuesto, RevisionGastoPrueba revision)
    {
        await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();
        await conexion.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.CatalogoPartida (Codigo, Nombre, IdTipoPartida, Nivel)
            SELECT @codigo, N'Otra partida de la revisión', IdTipoPartida, Nivel
            FROM ControlPresupuestario.CatalogoPartida WHERE IdCatalogoPartida = @partida;
            DECLARE @OtraPartida INT = CONVERT(INT, SCOPE_IDENTITY());

            DELETE FROM ControlPresupuestario.PresupuestoDetalle WHERE IdPresupuestoVersion = @version;
            INSERT INTO ControlPresupuestario.PresupuestoDetalle
                (IdPresupuestoVersion, IdCatalogoPartida, MontoPresupuestado)
            VALUES (@version, @OtraPartida, 0);
            """, new { codigo = Guid.NewGuid().ToString("N"), partida = presupuesto.IdPartida, version = revision.IdPresupuestoVersion });
    }

    private static async Task<bool> EsperarBloqueoPresupuestarioAsync(int sesion, int bloqueador)
    {
        await using SqlConnection conexion = await DbHelpers.OpenTestConnectionAsync();
        Stopwatch tiempo = Stopwatch.StartNew();
        while (tiempo.Elapsed < TimeSpan.FromSeconds(5))
        {
            int? sesionBloqueadora = await conexion.QuerySingleOrDefaultAsync<int?>("""
                SELECT blocking_session_id FROM sys.dm_exec_requests WHERE session_id = @sesion;
                """, new { sesion });
            if (sesionBloqueadora == bloqueador)
                return true;

            await Task.Delay(50);
        }

        return false;
    }

    private sealed class PresupuestoGastoPrueba
    {
        public int IdPresupuesto { get; set; }
        public int IdDetalleOriginal { get; set; }
        public int IdPartida { get; set; }
        public int IdMoneda { get; set; }
    }

    private sealed class RevisionGastoPrueba
    {
        public int IdPresupuestoVersion { get; set; }
        public int IdPresupuestoDetalle { get; set; }
    }

    private sealed class GastoVersionPrueba
    {
        public int IdPresupuestoDetalle { get; set; }
        public string Estado { get; set; } = string.Empty;
    }

    private sealed class ControlVigenteGastoPrueba
    {
        public decimal MontoEjecutado { get; set; }
        public decimal SaldoDisponible { get; set; }
    }
}
