using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

/// <summary>
/// Contratos de escritura contra SQL Server del contenedor de integración.
/// Ejecutar únicamente cuando se autorice aplicar migraciones al entorno de pruebas:
/// GlobalSetupFixture arranca la aplicación y DbUp antes de cualquier test.
/// </summary>
[NonParallelizable]
public class ControlPresupuestarioSpsTests : IntegrationTestBase
{
    private const string Schema = "ControlPresupuestario.";
    private int _idCentro;
    private int _idMoneda;
    private int _idPartida;

    [SetUp]
    public async Task PrepararCatalogosYPartida()
    {
        await using var connection = await AbrirConexion();
        // Respawn limpia también los catálogos presupuestarios. Resolver siempre
        // por código y recrear solo las referencias que esta fixture necesita.
        await connection.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.EstadoPresupuesto (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES
                ('BORRADOR'), ('APROBADO'), ('HISTORICO'), ('ANULADO')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.EstadoPresupuesto e
                WHERE e.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoMovimientoPresupuestal (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES
                ('COMPROMISO'), ('LIBERACION'), ('EJECUCION'), ('AJUSTE')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoMovimientoPresupuestal t
                WHERE t.Codigo = v.Codigo);
            IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.Moneda WHERE Codigo = 'PEN')
                INSERT INTO ControlPresupuestario.Moneda (Codigo, Nombre, Simbolo)
                VALUES ('PEN', N'Sol', N'S/');
            IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = 'PROYECTO')
                INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo, Nombre)
                VALUES ('PROYECTO', N'Proyecto');
            IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES')
                INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre)
                VALUES ('MATERIALES', N'Materiales');
            """);
        _idMoneda = await connection.QuerySingleAsync<int>(
            "SELECT IdMoneda FROM ControlPresupuestario.Moneda WHERE Codigo = 'PEN'");
        _idCentro = await connection.QuerySingleAsync<int>("""
            INSERT INTO ControlPresupuestario.CentroCosto (Codigo, Nombre, IdTipoCentroCosto)
            VALUES (@Codigo, N'Centro de pruebas',
                (SELECT IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = 'PROYECTO'));
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { Codigo = Guid.NewGuid().ToString("N")[..30] });
        _idPartida = await CrearPartida(connection);
    }

    [Test]
    public async Task Crear_NaceConV1Borrador_YRechazaCodigoDuplicado()
    {
        await using var connection = await AbrirConexion();
        var codigo = Guid.NewGuid().ToString("N");
        var resultado = await Crear(connection, codigo: codigo);
        Assert.That(resultado.NumeroVersion, Is.EqualTo(1));
        Assert.That(resultado.EstadoPresupuesto, Is.EqualTo("BORRADOR"));
        Assert.That(await connection.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoVersion
            WHERE IdPresupuesto = @IdPresupuesto AND NumeroVersion = 1
                AND FechaAprobacion IS NULL AND UsuarioAprobacion IS NULL;
            """, resultado), Is.EqualTo(1));
        Assert.That(await connection.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoDetalle
            WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, resultado), Is.Zero);
        Error(51205, async () => { await Crear(connection, codigo: codigo); });
    }

    [Test]
    public async Task Crear_TransaccionExterna_NoConfirmaPresupuestoNiV1()
    {
        await using var connection = await AbrirConexion();
        using var transaction = connection.BeginTransaction();
        var resultado = await Crear(connection, transaction);
        Assert.That(await connection.QuerySingleAsync<int>("SELECT @@TRANCOUNT", transaction: transaction),
            Is.EqualTo(1));
        transaction.Rollback();
        Assert.That(await connection.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.Presupuesto WHERE IdPresupuesto = @IdPresupuesto;
            """, resultado), Is.Zero);
        Assert.That(await connection.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoVersion
            WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, resultado), Is.Zero);
    }

    [Test]
    public async Task Versiones_RechazanSnapshotVacioYSinBaseAprobada()
    {
        await using var connection = await AbrirConexion();
        var presupuesto = await Crear(connection);
        Error(51209, async () => { await Aprobar(connection, presupuesto.IdPresupuestoVersion); });
        Error(51210, async () => { await CrearNueva(connection, presupuesto.IdPresupuesto); });
        Assert.That(await Estado(connection, presupuesto.IdPresupuestoVersion), Is.EqualTo("BORRADOR"));
    }

    [TestCase(false, 51212)]
    [TestCase(true, 51213)]
    public async Task Versiones_RechazanPartidaInactivaOConHijoInclusoInactivo(bool conHijo, int error)
    {
        await using var connection = await AbrirConexion();
        var (presupuesto, _) = await CrearAprobado(connection);
        var revision = await CrearNueva(connection, presupuesto.IdPresupuesto);
        if (conHijo)
        {
            var hija = await CrearPartida(connection);
            await connection.ExecuteAsync("""
                UPDATE ControlPresupuestario.CatalogoPartida
                SET IdPartidaPadre = @padre, Nivel = 2, Activo = 0 WHERE IdCatalogoPartida = @hija;
                """, new { padre = _idPartida, hija });
        }
        else
        {
            await connection.ExecuteAsync("""
                UPDATE ControlPresupuestario.CatalogoPartida SET Activo = 0 WHERE IdCatalogoPartida = @id;
                """, new { id = _idPartida });
        }
        Error(error, async () => { await Aprobar(connection, revision.IdPresupuestoVersion); });
        Assert.That(await Estado(connection, presupuesto.IdPresupuestoVersion), Is.EqualTo("APROBADO"));
        await connection.ExecuteAsync("""
            UPDATE ControlPresupuestario.PresupuestoVersion
            SET IdEstadoPresupuesto = (SELECT IdEstadoPresupuesto FROM ControlPresupuestario.EstadoPresupuesto
                WHERE Codigo = 'ANULADO') WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, revision);
        Error(error, async () => { await CrearNueva(connection, presupuesto.IdPresupuesto); });
    }

    [Test]
    public async Task CrearNueva_CopiaSnapshotCompleto_NoLedger_NiNumerosAnulados()
    {
        await using var connection = await AbrirConexion();
        var presupuesto = await Crear(connection);
        var detalle = await AgregarDetalle(connection, presupuesto.IdPresupuestoVersion, _idPartida, 100m);
        var segundaPartida = await CrearPartida(connection);
        await AgregarDetalle(connection, presupuesto.IdPresupuestoVersion, segundaPartida, 0m);
        await Aprobar(connection, presupuesto.IdPresupuestoVersion);
        await Registrar(connection, detalle, monto: 10m);
        var revision = await CrearNueva(connection, presupuesto.IdPresupuesto);
        Assert.That(revision.NumeroVersion, Is.EqualTo(2));
        Assert.That(revision.CantidadDetallesCopiados, Is.EqualTo(2));
        var copiados = (await connection.QueryAsync<Detalle>("""
            SELECT IdPresupuestoDetalle, IdCatalogoPartida, MontoPresupuestado, Observacion
            FROM ControlPresupuestario.PresupuestoDetalle WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, revision)).ToList();
        Assert.That(copiados.Single(d => d.IdCatalogoPartida == _idPartida).IdPresupuestoDetalle,
            Is.Not.EqualTo(detalle));
        Assert.That(copiados.Select(d => d.MontoPresupuestado), Is.EquivalentTo(new[] { 100m, 0m }));
        Assert.That(copiados.All(d => d.Observacion == "Observación original"), Is.True);
        Assert.That(await connection.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.MovimientoPresupuestal m
            JOIN ControlPresupuestario.PresupuestoDetalle d ON d.IdPresupuestoDetalle = m.IdPresupuestoDetalle
            WHERE d.IdPresupuestoVersion = @IdPresupuestoVersion;
            """, revision), Is.Zero);
        Error(51211, async () => { await CrearNueva(connection, presupuesto.IdPresupuesto); });
        await connection.ExecuteAsync("""
            UPDATE ControlPresupuestario.PresupuestoDetalle SET MontoPresupuestado = 50
            WHERE IdPresupuestoVersion = @IdPresupuestoVersion AND IdCatalogoPartida = @IdPartida;
            UPDATE ControlPresupuestario.PresupuestoVersion
            SET IdEstadoPresupuesto = (SELECT IdEstadoPresupuesto FROM ControlPresupuestario.EstadoPresupuesto
                WHERE Codigo = 'ANULADO') WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, new { revision.IdPresupuestoVersion, IdPartida = _idPartida });
        Assert.That(await connection.QuerySingleAsync<decimal>("""
            SELECT MontoPresupuestado FROM ControlPresupuestario.PresupuestoDetalle
            WHERE IdPresupuestoDetalle = @detalle;
            """, new { detalle }), Is.EqualTo(100m));
        var siguiente = await CrearNueva(connection, presupuesto.IdPresupuesto);
        Assert.That(siguiente.NumeroVersion, Is.EqualTo(3));
    }

    [TestCase("COMPROMISO", null, null)]
    [TestCase("LIBERACION", null, null)]
    [TestCase("EJECUCION", null, null)]
    [TestCase("AJUSTE", "COMPROMISO", "INCREMENTO")]
    [TestCase("AJUSTE", "COMPROMISO", "DECREMENTO")]
    [TestCase("AJUSTE", "EJECUCION", "INCREMENTO")]
    [TestCase("AJUSTE", "EJECUCION", "DECREMENTO")]
    public async Task Aprobar_NoPermiteOmitirPartidaConNetoPositivoONegativo(
        string tipo, string? afectacion, string? direccion)
    {
        await using var connection = await AbrirConexion();
        var (presupuesto, detalle) = await CrearAprobado(connection);
        await Registrar(connection, detalle, tipo: tipo, afectacion: afectacion, direccion: direccion, monto: 0.01m);
        var revision = await CrearNueva(connection, presupuesto.IdPresupuesto);
        await SustituirPartida(connection, revision.IdPresupuestoVersion);
        Error(51214, async () => { await Aprobar(connection, revision.IdPresupuestoVersion); });
        Assert.That(await Estado(connection, presupuesto.IdPresupuestoVersion), Is.EqualTo("APROBADO"));
        Assert.That(await Estado(connection, revision.IdPresupuestoVersion), Is.EqualTo("BORRADOR"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Aprobar_NetosCero_PermiteOmitirPartidaSinFallback(bool conReferenciaOperativa)
    {
        await using var connection = await AbrirConexion();
        var (presupuesto, detalle) = await CrearAprobado(connection);
        await Registrar(connection, detalle, tipo: "COMPROMISO", monto: 10m);
        await Registrar(connection, detalle, tipo: "LIBERACION", monto: 10m);
        await Registrar(connection, detalle, tipo: "EJECUCION", monto: 5m);
        await Registrar(connection, detalle, tipo: "AJUSTE", monto: 5m,
            afectacion: "EJECUCION", direccion: "DECREMENTO");
        if (conReferenciaOperativa)
        {
            // Una referencia activa no demuestra cierre ni define por sí sola
            // un estado económico pendiente. Aprobar no debe inferirlo aquí.
            await connection.ExecuteAsync("""
                INSERT INTO maestra.CategoriaGasto (Nombre) VALUES (@nombre);
                DECLARE @categoria INT = CONVERT(INT, SCOPE_IDENTITY());
                INSERT INTO contable.GastoAdministrativo
                    (IdCategoriaGasto, Fecha, Monto, IdPresupuestoDetalle)
                VALUES (@categoria, '20260917', 1, @detalle);
                """, new { nombre = "CP-Test-" + Guid.NewGuid().ToString("N"), detalle });
        }
        var revision = await CrearNueva(connection, presupuesto.IdPresupuesto);
        await SustituirPartida(connection, revision.IdPresupuestoVersion);
        await Aprobar(connection, revision.IdPresupuestoVersion);
        Assert.That(await Estado(connection, presupuesto.IdPresupuestoVersion), Is.EqualTo("HISTORICO"));
        Assert.That(await Estado(connection, revision.IdPresupuestoVersion), Is.EqualTo("APROBADO"));
    }

    [Test]
    public async Task Aprobar_NoCompensaCompromisoYEjecucionEntreSiParaCobertura()
    {
        await using var connection = await AbrirConexion();
        var (presupuesto, detalle) = await CrearAprobado(connection);
        await Registrar(connection, detalle, tipo: "COMPROMISO", monto: 1m);
        await Registrar(connection, detalle, tipo: "AJUSTE", monto: 1m,
            afectacion: "EJECUCION", direccion: "DECREMENTO");
        var revision = await CrearNueva(connection, presupuesto.IdPresupuesto);
        await SustituirPartida(connection, revision.IdPresupuestoVersion);
        Error(51214, async () => { await Aprobar(connection, revision.IdPresupuestoVersion); });
    }

    [Test]
    public async Task Aprobar_ConservaDetalleRealCeroYArrastre_AdmiteContinuacionHistorica()
    {
        await using var connection = await AbrirConexion();
        var (presupuesto, detalle) = await CrearAprobado(connection);
        await Registrar(connection, detalle, tipo: "EJECUCION", monto: 0.01m);
        var revision = await CrearNueva(connection, presupuesto.IdPresupuesto);
        await connection.ExecuteAsync("""
            UPDATE ControlPresupuestario.PresupuestoDetalle SET MontoPresupuestado = 0
            WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, revision);
        await Aprobar(connection, revision.IdPresupuestoVersion);
        await Registrar(connection, detalle, tipo: "EJECUCION", monto: 0.02m);
        var saldo = await connection.QuerySingleAsync<decimal>("""
            SELECT SaldoDisponible FROM ControlPresupuestario.vw_ControlPresupuestarioVigente
            WHERE IdPresupuesto = @IdPresupuesto AND IdCatalogoPartida = @IdPartida;
            """, new { presupuesto.IdPresupuesto, IdPartida = _idPartida });
        Assert.That(saldo, Is.EqualTo(-0.03m));
        Error(51207, async () => { await Aprobar(connection, revision.IdPresupuestoVersion); });
        var siguiente = await CrearNueva(connection, presupuesto.IdPresupuesto);
        await SustituirPartida(connection, siguiente.IdPresupuestoVersion);
        Error(51214, async () => { await Aprobar(connection, siguiente.IdPresupuestoVersion); });
        Assert.That(await Estado(connection, revision.IdPresupuestoVersion), Is.EqualTo("APROBADO"));
    }

    [TestCase("AJUSTE", null, null)]
    [TestCase("AJUSTE", "COMPROMISO", null)]
    [TestCase("AJUSTE", null, "INCREMENTO")]
    [TestCase("COMPROMISO", "COMPROMISO", "INCREMENTO")]
    [TestCase("LIBERACION", null, "DECREMENTO")]
    [TestCase("EJECUCION", "EJECUCION", null)]
    [TestCase("AJUSTE", "INVALIDO", "INCREMENTO")]
    public async Task Registrar_ValidaCoherenciaCruzadaSinDependerDeApi(
        string tipo, string? afectacion, string? direccion)
    {
        await using var connection = await AbrirConexion();
        var (_, detalle) = await CrearAprobado(connection);
        Error(51220, async () =>
        {
            await Registrar(connection, detalle, tipo: tipo, afectacion: afectacion, direccion: direccion);
        });
        Assert.That(await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM ControlPresupuestario.MovimientoPresupuestal WHERE IdPresupuestoDetalle = @detalle",
            new { detalle }), Is.Zero);
    }

    [Test]
    public async Task Registrar_RetryRecuperaFechaOriginal_ConflictoNoModificaLedger()
    {
        await using var connection = await AbrirConexion();
        var (_, detalle) = await CrearAprobado(connection);
        var clave = Guid.NewGuid().ToString("N");
        var original = await Registrar(connection, detalle, clave: clave, observacion: "Original");
        var retry = await Registrar(connection, detalle, clave: clave, observacion: "Original");
        Assert.That(original.EsNuevo, Is.True);
        Assert.That(retry.EsNuevo, Is.False);
        Assert.That(retry.IdMovimientoPresupuestal, Is.EqualTo(original.IdMovimientoPresupuestal));
        Assert.That(retry.Fecha, Is.EqualTo(original.Fecha));
        foreach (var observacion in new string?[] { "original", "Original ", "", null })
            Error(51230, async () => { await Registrar(connection, detalle, clave: clave, observacion: observacion); });
        Error(51230, async () =>
        {
            await Registrar(connection, detalle, clave: clave, observacion: "Original", monto: 2m);
        });
        Error(51230, async () =>
        {
            await Registrar(connection, detalle, clave: clave, observacion: "Original", fecha: original.Fecha.AddSeconds(1));
        });
        Assert.That(await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM ControlPresupuestario.MovimientoPresupuestal WHERE ClaveEvento = @clave",
            new { clave }), Is.EqualTo(1));
    }

    [Test]
    public async Task Registrar_DesactivacionPermiteRetryPeroNoMovimientoNuevo()
    {
        await using var connection = await AbrirConexion();
        var (presupuesto, detalle) = await CrearAprobado(connection);
        var clave = Guid.NewGuid().ToString("N");
        var original = await Registrar(connection, detalle, clave: clave);
        await connection.ExecuteAsync("""
            UPDATE ControlPresupuestario.Presupuesto SET Activo = 0 WHERE IdPresupuesto = @IdPresupuesto;
            UPDATE ControlPresupuestario.TipoMovimientoPresupuestal SET Activo = 0 WHERE Codigo = 'COMPROMISO';
            """, presupuesto);
        var retry = await Registrar(connection, detalle, clave: clave);
        Assert.That(retry.IdMovimientoPresupuestal, Is.EqualTo(original.IdMovimientoPresupuestal));
        Assert.That(retry.EsNuevo, Is.False);
        Error(51202, async () => { await Registrar(connection, detalle); });
    }

    [Test]
    public async Task Registrar_ClaveGlobal_NoEsUnicaSoloPorPresupuesto()
    {
        await using var connection = await AbrirConexion();
        var (_, detalle1) = await CrearAprobado(connection);
        var (_, detalle2) = await CrearAprobado(connection);
        var clave = Guid.NewGuid().ToString("N");
        await Registrar(connection, detalle1, clave: clave);
        Error(51230, async () => { await Registrar(connection, detalle2, clave: clave); });
    }

    [Test]
    public async Task Composicion_RevisionAprobacionYMovimientos_NoConfirmanTransaccionExterna()
    {
        await using var connection = await AbrirConexion();
        var (presupuesto, detalle) = await CrearAprobado(connection);
        using var transaction = connection.BeginTransaction();
        var revision = await CrearNueva(connection, presupuesto.IdPresupuesto, transaction);
        await Aprobar(connection, revision.IdPresupuestoVersion, transaction);
        await Registrar(connection, detalle, tipo: "LIBERACION", transaction: transaction);
        await Registrar(connection, detalle, tipo: "EJECUCION", transaction: transaction);
        Assert.That(await connection.QuerySingleAsync<int>("SELECT @@TRANCOUNT", transaction: transaction),
            Is.EqualTo(1));
        transaction.Rollback();
        Assert.That(await Estado(connection, presupuesto.IdPresupuestoVersion), Is.EqualTo("APROBADO"));
        Assert.That(await connection.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoVersion
            WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
            """, revision), Is.Zero);
        Assert.That(await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM ControlPresupuestario.MovimientoPresupuestal WHERE IdPresupuestoDetalle = @detalle",
            new { detalle }), Is.Zero);
    }

    [Test]
    public async Task Composicion_FallaEjecucion_PropietarioRevierteLiberacion()
    {
        await using var connection = await AbrirConexion();
        var (_, detalle) = await CrearAprobado(connection);
        using var transaction = connection.BeginTransaction();
        await Registrar(connection, detalle, tipo: "LIBERACION", transaction: transaction);
        Error(51221, async () =>
        {
            await Registrar(connection, detalle, tipo: "EJECUCION", monto: 0m, transaction: transaction);
        });
        transaction.Rollback();
        Assert.That(await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM ControlPresupuestario.MovimientoPresupuestal WHERE IdPresupuestoDetalle = @detalle",
            new { detalle }), Is.Zero);
    }

    [Test]
    public async Task Concurrencia_ClaveIgual_UnaInsercionYUnRetry()
    {
        await using var connection = await AbrirConexion();
        var (_, detalle) = await CrearAprobado(connection);
        var clave = Guid.NewGuid().ToString("N");
        await using var otra = await AbrirConexion();
        var resultados = await Task.WhenAll(
            Registrar(connection, detalle, clave: clave), Registrar(otra, detalle, clave: clave));
        Assert.That(resultados.Select(r => r.IdMovimientoPresupuestal).Distinct().Count(), Is.EqualTo(1));
        Assert.That(resultados.Count(r => r.EsNuevo), Is.EqualTo(1));
    }

    [Test]
    public async Task Concurrencia_CrearNueva_UnSoloBorrador()
    {
        await using var connection = await AbrirConexion();
        var (presupuesto, _) = await CrearAprobado(connection);
        await using var otra = await AbrirConexion();
        async Task<int> Intentar(SqlConnection conexion)
        {
            try { await CrearNueva(conexion, presupuesto.IdPresupuesto); return 0; }
            catch (SqlException ex) { return ex.Number; }
        }
        var resultados = await Task.WhenAll(Intentar(connection), Intentar(otra));
        Assert.That(resultados, Is.EquivalentTo(new[] { 0, 51211 }));
    }

    [Test]
    public async Task Concurrencia_Aprobar_UnExitoYUnRechazoPorEstado()
    {
        await using var connection = await AbrirConexion();
        var presupuesto = await Crear(connection);
        await AgregarDetalle(connection, presupuesto.IdPresupuestoVersion, _idPartida, 0m);
        await using var otra = await AbrirConexion();
        async Task<int> Intentar(SqlConnection conexion)
        {
            try { await Aprobar(conexion, presupuesto.IdPresupuestoVersion); return 0; }
            catch (SqlException ex) { return ex.Number; }
        }
        var resultados = await Task.WhenAll(Intentar(connection), Intentar(otra));
        Assert.That(resultados, Is.EquivalentTo(new[] { 0, 51207 }));
        Assert.That(await Estado(connection, presupuesto.IdPresupuestoVersion), Is.EqualTo("APROBADO"));
    }

    private static async Task<SqlConnection> AbrirConexion()
    {
        var connection = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    private Task<VersionResultado> Crear(SqlConnection connection, SqlTransaction? transaction = null, string? codigo = null) =>
        connection.QuerySingleAsync<VersionResultado>(Schema + "usp_Presupuesto_Crear",
            new { IdCentroCosto = _idCentro, IdMoneda = _idMoneda,
                Codigo = codigo ?? Guid.NewGuid().ToString("N"), Nombre = "Presupuesto de pruebas", UsuarioCreacion = "test-user" },
            transaction, commandType: CommandType.StoredProcedure);

    private static Task<VersionResultado> CrearNueva(SqlConnection connection, int id, SqlTransaction? transaction = null) =>
        connection.QuerySingleAsync<VersionResultado>(Schema + "usp_PresupuestoVersion_CrearNueva",
            new { IdPresupuesto = id, UsuarioCreacion = "test-user" }, transaction, commandType: CommandType.StoredProcedure);

    private static Task Aprobar(SqlConnection connection, int id, SqlTransaction? transaction = null) =>
        connection.QuerySingleAsync(Schema + "usp_PresupuestoVersion_Aprobar",
            new { IdPresupuestoVersion = id, UsuarioAprobacion = "test-user" }, transaction, commandType: CommandType.StoredProcedure);

    private static Task<MovimientoResultado> Registrar(SqlConnection connection, int detalle,
        string tipo = "COMPROMISO", decimal monto = 1m, string? clave = null,
        string? afectacion = null, string? direccion = null, string? observacion = null,
        DateTime? fecha = null, SqlTransaction? transaction = null) =>
        connection.QuerySingleAsync<MovimientoResultado>(Schema + "usp_MovimientoPresupuestal_Registrar",
            new { IdPresupuestoDetalle = detalle, TipoMovimiento = tipo, Monto = monto,
                ClaveEvento = clave ?? Guid.NewGuid().ToString("N"), Origen = "PRUEBA", IdOrigen = 1,
                Afectacion = afectacion, Direccion = direccion, Observacion = observacion, Fecha = fecha },
            transaction, commandType: CommandType.StoredProcedure);

    private async Task<(VersionResultado Presupuesto, int Detalle)> CrearAprobado(SqlConnection connection)
    {
        var presupuesto = await Crear(connection);
        var detalle = await AgregarDetalle(connection, presupuesto.IdPresupuestoVersion, _idPartida, 100m);
        await Aprobar(connection, presupuesto.IdPresupuestoVersion);
        return (presupuesto, detalle);
    }

    private static Task<int> CrearPartida(SqlConnection connection) => connection.QuerySingleAsync<int>("""
        INSERT INTO ControlPresupuestario.CatalogoPartida (Codigo, Nombre, IdTipoPartida, Nivel)
        VALUES (@Codigo, N'Partida de pruebas',
            (SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES'), 1);
        SELECT CONVERT(INT, SCOPE_IDENTITY());
        """, new { Codigo = Guid.NewGuid().ToString("N") });

    private static Task<int> AgregarDetalle(SqlConnection connection, int version, int partida, decimal monto) =>
        connection.QuerySingleAsync<int>("""
            INSERT INTO ControlPresupuestario.PresupuestoDetalle
                (IdPresupuestoVersion, IdCatalogoPartida, MontoPresupuestado, Observacion)
            VALUES (@version, @partida, @monto, N'Observación original');
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { version, partida, monto });

    private static async Task SustituirPartida(SqlConnection connection, int version)
    {
        var partida = await CrearPartida(connection);
        await connection.ExecuteAsync("""
            DELETE FROM ControlPresupuestario.PresupuestoDetalle WHERE IdPresupuestoVersion = @version;
            """, new { version });
        await AgregarDetalle(connection, version, partida, 0m);
    }

    private static Task<string> Estado(SqlConnection connection, int version) => connection.QuerySingleAsync<string>("""
        SELECT e.Codigo FROM ControlPresupuestario.PresupuestoVersion v
        JOIN ControlPresupuestario.EstadoPresupuesto e ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
        WHERE v.IdPresupuestoVersion = @version;
        """, new { version });

    private static void Error(int numero, Func<Task> accion)
    {
        var ex = Assert.ThrowsAsync<SqlException>(async () => await accion());
        Assert.That(ex!.Number, Is.EqualTo(numero));
    }

    private sealed class VersionResultado
    {
        public int IdPresupuesto { get; set; }
        public int IdPresupuestoVersion { get; set; }
        public int NumeroVersion { get; set; }
        public string EstadoPresupuesto { get; set; } = "";
        public int CantidadDetallesCopiados { get; set; }
    }

    private sealed class MovimientoResultado
    {
        public long IdMovimientoPresupuestal { get; set; }
        public DateTime Fecha { get; set; }
        public bool EsNuevo { get; set; }
    }

    private sealed class Detalle
    {
        public int IdPresupuestoDetalle { get; set; }
        public int IdCatalogoPartida { get; set; }
        public decimal MontoPresupuestado { get; set; }
        public string? Observacion { get; set; }
    }
}
