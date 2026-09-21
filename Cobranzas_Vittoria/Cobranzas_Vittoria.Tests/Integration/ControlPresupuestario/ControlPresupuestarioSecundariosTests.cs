using System.Data;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

// Misma fixture y catálogo por test que el núcleo. No ejecutar sin autorizar
// el arranque de DbUp en el contenedor desechable de GlobalSetupFixture.
public partial class ControlPresupuestarioSpsTests
{
    [Test]
    public async Task Detalle_EditarBorrador_CeroDuplicadoYEliminacion()
    {
        await using var cn = await AbrirConexion();
        var p = await Crear(cn);
        var d = await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 0m);
        Error(51215, async () => { await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 5m); });
        Error(51221, async () => { await ActualizarPorSp(cn, d.IdPresupuestoDetalle, -1m); });
        await ActualizarPorSp(cn, d.IdPresupuestoDetalle, 2m);
        await ActualizarPorSp(cn, d.IdPresupuestoDetalle, 0m);
        var lista = (await cn.QueryAsync<Detalle>(Schema + "usp_PresupuestoDetalle_ListarPorVersion",
            new { p.IdPresupuestoVersion }, commandType: CommandType.StoredProcedure)).ToList();
        Assert.That(lista.Single().MontoPresupuestado, Is.Zero);
        Assert.That(lista.Single().IdCatalogoPartida, Is.EqualTo(_idPartida));
        await EliminarPorSp(cn, d.IdPresupuestoDetalle);
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoDetalle WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, p), Is.Zero);
    }

    [TestCase("APROBADO")]
    [TestCase("HISTORICO")]
    [TestCase("ANULADO")]
    public async Task DetalleYAnulacion_NoModificanVersionesFueraDeBorrador(string estado)
    {
        await using var cn = await AbrirConexion();
        var p = await Crear(cn);
        var d = await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 5m);
        if (estado == "ANULADO") await AnularPorSp(cn, p.IdPresupuestoVersion);
        else
        {
            await Aprobar(cn, p.IdPresupuestoVersion);
            if (estado == "HISTORICO")
            {
                var v2 = await CrearNueva(cn, p.IdPresupuesto);
                await Aprobar(cn, v2.IdPresupuestoVersion);
            }
        }
        Error(51207, async () => { await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 0m); });
        Error(51207, async () => { await ActualizarPorSp(cn, d.IdPresupuestoDetalle, 0m); });
        Error(51207, async () => { await EliminarPorSp(cn, d.IdPresupuestoDetalle); });
        Error(51207, async () => { await AnularPorSp(cn, p.IdPresupuestoVersion); });
        Assert.That(await Estado(cn, p.IdPresupuestoVersion), Is.EqualTo(estado));
        Assert.That(await cn.QuerySingleAsync<decimal>("""
            SELECT MontoPresupuestado FROM ControlPresupuestario.PresupuestoDetalle WHERE IdPresupuestoDetalle = @IdPresupuestoDetalle;
            """, d), Is.EqualTo(5m));
    }

    [Test]
    public async Task Anular_ConservaDetallesYAuditoria_NoReutilizaNumero()
    {
        await using var cn = await AbrirConexion();
        var (p, _) = await CrearAprobado(cn);
        var fecha = await cn.QuerySingleAsync<DateTime>("""
            SELECT FechaAprobacion FROM ControlPresupuestario.PresupuestoVersion WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, p);
        var v2 = await CrearNueva(cn, p.IdPresupuesto);
        await AnularPorSp(cn, v2.IdPresupuestoVersion);
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoDetalle WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, v2), Is.EqualTo(1));
        var v3 = await CrearNueva(cn, p.IdPresupuesto);
        Assert.That(v3.NumeroVersion, Is.EqualTo(3));
        Assert.That(await Estado(cn, p.IdPresupuestoVersion), Is.EqualTo("APROBADO"));
        Assert.That(await cn.QuerySingleAsync<DateTime>("""
            SELECT FechaAprobacion FROM ControlPresupuestario.PresupuestoVersion WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, p), Is.EqualTo(fecha));
    }

    [Test]
    public async Task Catalogo_JerarquiaContextoYCiclos_ConservaHijosInactivos()
    {
        await using var cn = await AbrirConexion();
        var raiz = await CrearPartidaPorSp(cn);
        var hijo = await CrearPartidaPorSp(cn, raiz.IdCatalogoPartida);
        Assert.That(raiz.Nivel, Is.EqualTo(1));
        Assert.That(hijo.Nivel, Is.EqualTo(2));
        Error(51217, async () => { await ActualizarPartidaPorSp(cn, raiz, raiz.IdCatalogoPartida); });
        Error(51217, async () => { await ActualizarPartidaPorSp(cn, raiz, hijo.IdCatalogoPartida); });
        Error(51201, async () => { await CrearPartidaPorSp(cn, int.MaxValue); });
        await ActualizarPartidaPorSp(cn, hijo, raiz.IdCatalogoPartida, activo: false);
        var leida = await cn.QuerySingleAsync<PartidaResultado>(Schema + "usp_CatalogoPartida_Obtener",
            new { raiz.IdCatalogoPartida }, commandType: CommandType.StoredProcedure);
        Assert.That(leida.EsHoja, Is.False);
        var hijos = (await cn.QueryAsync<PartidaResultado>(Schema + "usp_CatalogoPartida_Listar",
            new { IdPartidaPadre = raiz.IdCatalogoPartida, Activo = false }, commandType: CommandType.StoredProcedure)).ToList();
        Assert.That(hijos.Single().IdCatalogoPartida, Is.EqualTo(hijo.IdCatalogoPartida));
        Assert.That(hijos.Single().NombrePartidaPadre, Is.EqualTo(raiz.Nombre));
        var p = await Crear(cn);
        Error(51213, async () => { await AgregarPorSp(cn, p.IdPresupuestoVersion, raiz.IdCatalogoPartida, 0m); });
        Error(51212, async () => { await AgregarPorSp(cn, p.IdPresupuestoVersion, hijo.IdCatalogoPartida, 0m); });
    }

    [Test]
    public async Task Catalogo_ReubicaHojaLibre_ProtegePartidaEnUsoYClasificacion()
    {
        await using var cn = await AbrirConexion();
        var raiz = await CrearPartidaPorSp(cn);
        var hoja = await CrearPartidaPorSp(cn);
        await ActualizarPartidaPorSp(cn, hoja, raiz.IdCatalogoPartida);
        var leida = await cn.QuerySingleAsync<PartidaResultado>(Schema + "usp_CatalogoPartida_Obtener",
            new { hoja.IdCatalogoPartida }, commandType: CommandType.StoredProcedure);
        Assert.That(leida.Nivel, Is.EqualTo(2));
        var p = await Crear(cn);
        await AgregarPorSp(cn, p.IdPresupuestoVersion, hoja.IdCatalogoPartida, 0m);
        Error(51218, async () => { await ActualizarPartidaPorSp(cn, hoja, null); });
        Error(51218, async () => { await CrearPartidaPorSp(cn, hoja.IdCatalogoPartida); });
        await ActualizarPartidaPorSp(cn, hoja, raiz.IdCatalogoPartida, activo: false);
        leida = await cn.QuerySingleAsync<PartidaResultado>(Schema + "usp_CatalogoPartida_Obtener",
            new { hoja.IdCatalogoPartida }, commandType: CommandType.StoredProcedure);
        Assert.That(leida.Activo, Is.False);
        Assert.That(leida.IdPartidaPadre, Is.EqualTo(raiz.IdCatalogoPartida));
    }

    [Test]
    public async Task Presupuesto_ActualizarDesactivarYReactivar_IdentidadEconomicaEstable()
    {
        await using var cn = await AbrirConexion();
        var (p, _) = await CrearAprobado(cn);
        await cn.QuerySingleAsync(Schema + "usp_Presupuesto_Actualizar",
            new { p.IdPresupuesto, Nombre = "Renombrado", Activo = false }, commandType: CommandType.StoredProcedure);
        var leido = await cn.QuerySingleAsync<PresupuestoLectura>(Schema + "usp_Presupuesto_Obtener",
            new { p.IdPresupuesto }, commandType: CommandType.StoredProcedure);
        Assert.That(leido.IdCentroCosto, Is.EqualTo(_idCentro));
        Assert.That(leido.IdMoneda, Is.EqualTo(_idMoneda));
        Assert.That(leido.Nombre, Is.EqualTo("Renombrado"));
        Assert.That(leido.Activo, Is.False);
        Error(51202, async () => { await CrearNueva(cn, p.IdPresupuesto); });
        // El contrato SQL no expone moneda, centro ni código como parámetros mutables.
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.parameters
            WHERE object_id = OBJECT_ID('ControlPresupuestario.usp_Presupuesto_Actualizar')
                AND name IN ('@IdMoneda', '@IdCentroCosto', '@Codigo');
            """), Is.Zero);
        await cn.QuerySingleAsync(Schema + "usp_Presupuesto_Actualizar",
            new { p.IdPresupuesto, Nombre = "Renombrado", Activo = true }, commandType: CommandType.StoredProcedure);
        await CrearNueva(cn, p.IdPresupuesto);
    }

    [Test]
    public async Task Lecturas_PresupuestoVersionDetalleYLedger_IdentificanOrigenReal()
    {
        await using var cn = await AbrirConexion();
        var p = await Crear(cn);
        var elaboracion = await cn.QuerySingleAsync<PresupuestoLectura>(Schema + "usp_Presupuesto_Obtener",
            new { p.IdPresupuesto }, commandType: CommandType.StoredProcedure);
        Assert.That(elaboracion.EstadoElaboracion, Is.EqualTo("EN_ELABORACION"));
        var d = await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 10m);
        await Aprobar(cn, p.IdPresupuestoVersion);
        var movimiento = await Registrar(cn, d.IdPresupuestoDetalle);
        var v2 = await CrearNueva(cn, p.IdPresupuesto);
        var listado = (await cn.QueryAsync<PresupuestoLectura>(Schema + "usp_Presupuesto_Listar",
            new { IdCentroCosto = _idCentro, IdMoneda = _idMoneda, Activo = true }, commandType: CommandType.StoredProcedure)).Single();
        Assert.That(listado.EstadoElaboracion, Is.EqualTo("EN_REVISION"));
        Assert.That(listado.IdPresupuestoVersionAprobada, Is.EqualTo(p.IdPresupuestoVersion));
        Assert.That(listado.IdPresupuestoVersionBorrador, Is.EqualTo(v2.IdPresupuestoVersion));
        var versiones = (await cn.QueryAsync<VersionResultado>(Schema + "usp_PresupuestoVersion_Listar",
            new { p.IdPresupuesto }, commandType: CommandType.StoredProcedure)).ToList();
        Assert.That(versiones.Select(v => v.NumeroVersion), Is.EqualTo(new[] { 2, 1 }));
        await Aprobar(cn, v2.IdPresupuestoVersion);
        var historica = await cn.QuerySingleAsync<VersionResultado>(Schema + "usp_PresupuestoVersion_Obtener",
            new { p.IdPresupuestoVersion }, commandType: CommandType.StoredProcedure);
        Assert.That(historica.EstadoPresupuesto, Is.EqualTo("HISTORICO"));
        var ledger = await cn.QuerySingleAsync<LedgerLectura>(Schema + "usp_MovimientoPresupuestal_Obtener",
            new { movimiento.IdMovimientoPresupuestal }, commandType: CommandType.StoredProcedure);
        Assert.That(ledger.IdPresupuestoVersion, Is.EqualTo(p.IdPresupuestoVersion));
        Assert.That(ledger.IdPresupuestoDetalle, Is.EqualTo(d.IdPresupuestoDetalle));
        var historial = await cn.QueryAsync<LedgerLectura>(Schema + "usp_MovimientoPresupuestal_ListarPorDetalle",
            new { d.IdPresupuestoDetalle }, commandType: CommandType.StoredProcedure);
        Assert.That(historial.Single().IdMovimientoPresupuestal, Is.EqualTo(movimiento.IdMovimientoPresupuestal));
        Assert.That(await cn.QueryAsync<PresupuestoLectura>(Schema + "usp_Presupuesto_Obtener",
            new { IdPresupuesto = int.MaxValue }, commandType: CommandType.StoredProcedure), Is.Empty);
    }

    [TestCase("EstadoPresupuesto")]
    [TestCase("TipoCentroCosto")]
    [TestCase("TipoPartida")]
    [TestCase("TipoMovimientoPresupuestal")]
    [TestCase("Moneda")]
    public async Task Lecturas_CatalogosEstructurales_ExponenCodigoYActivo(string entidad)
    {
        await using var cn = await AbrirConexion();
        var filas = (await cn.QueryAsync<CatalogoLectura>(Schema + "usp_" + entidad + "_Listar",
            new { Activo = true }, commandType: CommandType.StoredProcedure)).ToList();
        Assert.That(filas, Is.Not.Empty);
        Assert.That(filas.All(f => f.Activo && !string.IsNullOrEmpty(f.Codigo)), Is.True);
    }

    [Test]
    public async Task CentroCosto_AltaEdicionDesactivacionYLectura_SinPerderPresupuesto()
    {
        await using var cn = await AbrirConexion();
        var tipo = await cn.QuerySingleAsync<int>(
            "SELECT IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = 'PROYECTO'");
        var codigo = Guid.NewGuid().ToString("N")[..30];
        var centro = await cn.QuerySingleAsync<CentroResultado>(Schema + "usp_CentroCosto_Crear",
            new { Codigo = codigo, Nombre = "Centro", IdTipoCentroCosto = tipo,
                IdProyecto = SeedIds.ProyectoMaytaCapacII }, commandType: CommandType.StoredProcedure);
        Error(51205, async () => { await cn.QuerySingleAsync(Schema + "usp_CentroCosto_Crear",
            new { Codigo = codigo, Nombre = "Otro", IdTipoCentroCosto = tipo,
                IdProyecto = SeedIds.ProyectoMaytaCapacII }, commandType: CommandType.StoredProcedure); });
        _idCentro = centro.IdCentroCosto;
        var p = await Crear(cn);
        await cn.QuerySingleAsync(Schema + "usp_CentroCosto_Actualizar",
            new { centro.IdCentroCosto, Nombre = "Retirado", Activo = false }, commandType: CommandType.StoredProcedure);
        var leido = await cn.QuerySingleAsync<CentroResultado>(Schema + "usp_CentroCosto_Obtener",
            new { centro.IdCentroCosto }, commandType: CommandType.StoredProcedure);
        Assert.That(leido.Activo, Is.False);
        var retirados = await cn.QueryAsync<CentroResultado>(Schema + "usp_CentroCosto_Listar",
            new { Activo = false, IdTipoCentroCosto = tipo, Busqueda = codigo }, commandType: CommandType.StoredProcedure);
        Assert.That(retirados.Single().IdCentroCosto, Is.EqualTo(centro.IdCentroCosto));
        Error(51202, async () => { await Crear(cn); });
        Assert.That(await Estado(cn, p.IdPresupuestoVersion), Is.EqualTo("BORRADOR"));
    }

    [Test]
    public async Task Secundarios_TransaccionExterna_RevierteEdicionYAnulacion()
    {
        await using var cn = await AbrirConexion();
        var p = await Crear(cn);
        using var tx = cn.BeginTransaction();
        var d = await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 1m, tx);
        await ActualizarPorSp(cn, d.IdPresupuestoDetalle, 0m, tx);
        await EliminarPorSp(cn, d.IdPresupuestoDetalle, tx);
        await AnularPorSp(cn, p.IdPresupuestoVersion, tx);
        await cn.QuerySingleAsync(Schema + "usp_Presupuesto_Actualizar",
            new { p.IdPresupuesto, Nombre = "Provisional", Activo = false }, tx, commandType: CommandType.StoredProcedure);
        Assert.That(await cn.QuerySingleAsync<int>("SELECT @@TRANCOUNT", transaction: tx), Is.EqualTo(1));
        tx.Rollback();
        Assert.That(await Estado(cn, p.IdPresupuestoVersion), Is.EqualTo("BORRADOR"));
        Assert.That(await cn.QuerySingleAsync<bool>(
            "SELECT Activo FROM ControlPresupuestario.Presupuesto WHERE IdPresupuesto = @IdPresupuesto", p), Is.True);
    }

    [Test]
    public async Task Maestros_TransaccionExterna_NoConfirmanAltasNiEdiciones()
    {
        await using var cn = await AbrirConexion();
        var tipoCentro = await cn.QuerySingleAsync<int>(
            "SELECT IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = 'PROYECTO'");
        var tipoPartida = await cn.QuerySingleAsync<int>(
            "SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES'");
        using var tx = cn.BeginTransaction();
        var centro = await cn.QuerySingleAsync<CentroResultado>(Schema + "usp_CentroCosto_Crear",
            new { Codigo = Guid.NewGuid().ToString("N")[..30], Nombre = "Temporal",
                IdTipoCentroCosto = tipoCentro, IdProyecto = SeedIds.ProyectoMaytaCapacII },
            tx, commandType: CommandType.StoredProcedure);
        await cn.QuerySingleAsync(Schema + "usp_CentroCosto_Actualizar",
            new { centro.IdCentroCosto, Nombre = "Actualizado", Activo = false }, tx, commandType: CommandType.StoredProcedure);
        var partida = await cn.QuerySingleAsync<PartidaResultado>(Schema + "usp_CatalogoPartida_Crear",
            new { Codigo = Guid.NewGuid().ToString("N"), Nombre = "Temporal", IdTipoPartida = tipoPartida },
            tx, commandType: CommandType.StoredProcedure);
        await cn.QuerySingleAsync(Schema + "usp_CatalogoPartida_Actualizar",
            new { partida.IdCatalogoPartida, partida.IdTipoPartida, Nombre = "Actualizado", Activo = false },
            tx, commandType: CommandType.StoredProcedure);
        Assert.That(await cn.QuerySingleAsync<int>("SELECT @@TRANCOUNT", transaction: tx), Is.EqualTo(1));
        tx.Rollback();
        Assert.That(await cn.QueryAsync<CentroResultado>(Schema + "usp_CentroCosto_Obtener",
            new { centro.IdCentroCosto }, commandType: CommandType.StoredProcedure), Is.Empty);
        Assert.That(await cn.QueryAsync<PartidaResultado>(Schema + "usp_CatalogoPartida_Obtener",
            new { partida.IdCatalogoPartida }, commandType: CommandType.StoredProcedure), Is.Empty);
    }

    [Test]
    public async Task Detalle_EliminarConReferenciaIndebida_RespetaFkSinBorrarLedger()
    {
        await using var cn = await AbrirConexion();
        var p = await Crear(cn);
        var d = await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 0m);
        // Simula un escritor externo que omitió la validación BORRADOR del núcleo.
        await cn.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.MovimientoPresupuestal
                (IdPresupuestoDetalle, IdTipoMovimientoPresupuestal, ClaveEvento, Origen, IdOrigen, Monto)
            VALUES (@IdPresupuestoDetalle,
                (SELECT IdTipoMovimientoPresupuestal FROM ControlPresupuestario.TipoMovimientoPresupuestal
                    WHERE Codigo = 'COMPROMISO'), @clave, 'PRUEBA', 1, 1);
            """, new { d.IdPresupuestoDetalle, clave = Guid.NewGuid().ToString("N") });
        Error(547, async () => { await EliminarPorSp(cn, d.IdPresupuestoDetalle); });
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoDetalle d
            JOIN ControlPresupuestario.MovimientoPresupuestal m ON m.IdPresupuestoDetalle = d.IdPresupuestoDetalle
            WHERE d.IdPresupuestoDetalle = @IdPresupuestoDetalle;
            """, d), Is.EqualTo(1));
    }

    [Test]
    public async Task Catalogo_Crear_RechazaTipoInexistenteYRetirado()
    {
        await using var cn = await AbrirConexion();
        Error(51201, async () => { await cn.QuerySingleAsync(Schema + "usp_CatalogoPartida_Crear",
            new { Codigo = Guid.NewGuid().ToString("N"), Nombre = "Inválido", IdTipoPartida = int.MaxValue },
            commandType: CommandType.StoredProcedure); });
        await cn.ExecuteAsync("UPDATE ControlPresupuestario.TipoPartida SET Activo = 0 WHERE Codigo = 'MATERIALES'");
        Error(51202, async () => { await CrearPartidaPorSp(cn); });
    }

    [TestCase("Actualizar")]
    [TestCase("Eliminar")]
    [TestCase("Agregar")]
    [TestCase("Anular")]
    public async Task Concurrencia_AprobarVsEdicion_RespetaOrdenSerial(string accion)
    {
        await using var cn = await AbrirConexion();
        var p = await Crear(cn);
        var d = await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 1m);
        var otraPartida = await CrearPartida(cn);
        await using var otra = await AbrirConexion();
        var resultados = await Task.WhenAll(
            IntentarOperacion(() => Aprobar(cn, p.IdPresupuestoVersion)),
            IntentarOperacion(async () =>
            {
                switch (accion)
                {
                    case "Actualizar": await ActualizarPorSp(otra, d.IdPresupuestoDetalle, 9m); break;
                    case "Eliminar": await EliminarPorSp(otra, d.IdPresupuestoDetalle); break;
                    case "Agregar": await AgregarPorSp(otra, p.IdPresupuestoVersion, otraPartida, 2m); break;
                    case "Anular": await AnularPorSp(otra, p.IdPresupuestoVersion); break;
                }
            }));
        var estado = await Estado(cn, p.IdPresupuestoVersion);
        if (resultados[0] == 0)
        {
            Assert.That(estado, Is.EqualTo("APROBADO"));
            Assert.That(resultados[1], Is.AnyOf(0, 51207));
            if (accion is "Eliminar" or "Anular") Assert.That(resultados[1], Is.EqualTo(51207));
            if (accion == "Actualizar")
                Assert.That(await cn.QuerySingleAsync<decimal>("""
                    SELECT MontoPresupuestado FROM ControlPresupuestario.PresupuestoDetalle WHERE IdPresupuestoDetalle = @IdPresupuestoDetalle;
                    """, d), Is.EqualTo(resultados[1] == 0 ? 9m : 1m));
            if (accion == "Agregar")
                Assert.That(await cn.QuerySingleAsync<int>("""
                    SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoDetalle WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
                    """, p), Is.EqualTo(resultados[1] == 0 ? 2 : 1));
        }
        else
        {
            Assert.That(resultados[1], Is.Zero);
            Assert.That(accion, Is.AnyOf("Eliminar", "Anular"));
            Assert.That(resultados[0], Is.EqualTo(accion == "Eliminar" ? 51209 : 51207));
            Assert.That(estado, Is.EqualTo(accion == "Eliminar" ? "BORRADOR" : "ANULADO"));
        }
    }

    [Test]
    public async Task Concurrencia_CrearNuevaVsAnular_NoReutilizaNumeroNiDuplicaBorrador()
    {
        await using var cn = await AbrirConexion();
        var (p, _) = await CrearAprobado(cn);
        var v2 = await CrearNueva(cn, p.IdPresupuesto);
        await using var otra = await AbrirConexion();
        var resultados = await Task.WhenAll(
            IntentarOperacion(async () => { await CrearNueva(cn, p.IdPresupuesto); }),
            IntentarOperacion(() => AnularPorSp(otra, v2.IdPresupuestoVersion)));
        Assert.That(resultados[0], Is.AnyOf(0, 51211));
        Assert.That(resultados[1], Is.Zero);
        Assert.That(await Estado(cn, v2.IdPresupuestoVersion), Is.EqualTo("ANULADO"));
        if (resultados[0] == 51211)
            Assert.That((await CrearNueva(cn, p.IdPresupuesto)).NumeroVersion, Is.EqualTo(3));
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoVersion v
            JOIN ControlPresupuestario.EstadoPresupuesto e ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
            WHERE v.IdPresupuesto = @IdPresupuesto AND e.Codigo = 'BORRADOR' AND v.NumeroVersion = 3;
            """, p), Is.EqualTo(1));
    }

    private static async Task<int> IntentarOperacion(Func<Task> accion)
    {
        try { await accion(); return 0; }
        catch (SqlException ex) { return ex.Number; }
    }

    private static Task<Detalle> AgregarPorSp(SqlConnection cn, int version, int partida, decimal monto,
        SqlTransaction? tx = null) => cn.QuerySingleAsync<Detalle>(Schema + "usp_PresupuestoDetalle_Agregar",
        new { IdPresupuestoVersion = version, IdCatalogoPartida = partida, MontoPresupuestado = monto }, tx,
        commandType: CommandType.StoredProcedure);

    private static Task ActualizarPorSp(SqlConnection cn, int detalle, decimal monto, SqlTransaction? tx = null) =>
        cn.QuerySingleAsync(Schema + "usp_PresupuestoDetalle_Actualizar",
            new { IdPresupuestoDetalle = detalle, MontoPresupuestado = monto }, tx, commandType: CommandType.StoredProcedure);

    private static Task EliminarPorSp(SqlConnection cn, int detalle, SqlTransaction? tx = null) =>
        cn.QuerySingleAsync(Schema + "usp_PresupuestoDetalle_Eliminar",
            new { IdPresupuestoDetalle = detalle }, tx, commandType: CommandType.StoredProcedure);

    private static Task AnularPorSp(SqlConnection cn, int version, SqlTransaction? tx = null) =>
        cn.QuerySingleAsync(Schema + "usp_PresupuestoVersion_Anular",
            new { IdPresupuestoVersion = version }, tx, commandType: CommandType.StoredProcedure);

    private static async Task<PartidaResultado> CrearPartidaPorSp(SqlConnection cn, int? padre = null)
    {
        var tipo = await cn.QuerySingleAsync<int>(
            "SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES'");
        return await cn.QuerySingleAsync<PartidaResultado>(Schema + "usp_CatalogoPartida_Crear",
            new { Codigo = Guid.NewGuid().ToString("N"), Nombre = "Partida", IdTipoPartida = tipo, IdPartidaPadre = padre },
            commandType: CommandType.StoredProcedure);
    }

    private static Task ActualizarPartidaPorSp(SqlConnection cn, PartidaResultado partida, int? padre, bool activo = true) =>
        cn.QuerySingleAsync(Schema + "usp_CatalogoPartida_Actualizar",
            new { partida.IdCatalogoPartida, partida.Nombre, partida.IdTipoPartida, IdPartidaPadre = padre, Activo = activo },
            commandType: CommandType.StoredProcedure);

    private sealed class PartidaResultado
    {
        public int IdCatalogoPartida { get; set; }
        public int IdTipoPartida { get; set; }
        public int? IdPartidaPadre { get; set; }
        public string Nombre { get; set; } = "";
        public string? NombrePartidaPadre { get; set; }
        public int Nivel { get; set; }
        public bool Activo { get; set; }
        public bool EsHoja { get; set; }
    }

    private sealed class PresupuestoLectura
    {
        public int IdCentroCosto { get; set; }
        public int IdMoneda { get; set; }
        public string Nombre { get; set; } = "";
        public bool Activo { get; set; }
        public string EstadoElaboracion { get; set; } = "";
        public int? IdPresupuestoVersionAprobada { get; set; }
        public int? IdPresupuestoVersionBorrador { get; set; }
    }

    private sealed class LedgerLectura
    {
        public long IdMovimientoPresupuestal { get; set; }
        public int IdPresupuestoDetalle { get; set; }
        public int IdPresupuestoVersion { get; set; }
    }

    private sealed class CatalogoLectura
    {
        public string Codigo { get; set; } = "";
        public bool Activo { get; set; }
    }

    private sealed class CentroResultado
    {
        public int IdCentroCosto { get; set; }
        public bool Activo { get; set; }
    }
}
