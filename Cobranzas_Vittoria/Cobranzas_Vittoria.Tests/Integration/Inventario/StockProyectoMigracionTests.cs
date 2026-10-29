using System.Data;
using Cobranzas_Vittoria.Inventario;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Dapper;
using DbUp;
using DbUp.Engine;
using DbUp.Helpers;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Inventario;

[NonParallelizable]
public class StockProyectoMigracionTests
{
    private const string MIGRACION = "V2_8_2__Inventario_Retira_Inicializacion_StockProyecto.sql";
    private string _baseDatos = null!;
    private string _cadena = null!;

    [SetUp]
    public async Task CrearBaseAnteriorALaLimpieza()
    {
        _baseDatos = "stock_proyecto_migracion_" + Guid.NewGuid().ToString("N");
        await using SqlConnection master = new(GlobalSetupFixture.DbContainer.GetConnectionString());
        await master.ExecuteAsync($"CREATE DATABASE [{_baseDatos}]");
        _cadena = new SqlConnectionStringBuilder(GlobalSetupFixture.DbContainer.GetConnectionString())
        {
            InitialCatalog = _baseDatos
        }.ConnectionString;

        DatabaseUpgradeResult resultado = Migrar(nombre => nombre.Contains(".Migrations.Versioned.")
            && !nombre.EndsWith(MIGRACION, StringComparison.Ordinal));
        Assert.That(resultado.Successful, Is.True, resultado.Error?.ToString());
    }

    [TearDown]
    public async Task EliminarBaseDesechable()
    {
        if (_baseDatos is null) return;

        SqlConnection.ClearAllPools();
        await using SqlConnection master = new(GlobalSetupFixture.DbContainer.GetConnectionString());
        await master.ExecuteAsync($"ALTER DATABASE [{_baseDatos}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_baseDatos}]");
    }

    [Test]
    public async Task Limpieza_BaseVacia_PermiteOperarSinActivacionYNoRecreaObjetos()
    {
        await AplicarLimpiezaYRepeatables();
        await using SqlConnection db = new(_cadena);
        await PrepararMaestros(db);
        await RegistrarEntrada(db, 1.25m);

        Assert.That(await db.QuerySingleAsync<decimal>("SELECT Stock FROM almacen.KardexStock"), Is.EqualTo(1.25m));
        Assert.That(await db.QuerySingleAsync<decimal>("SELECT Stock FROM almacen.StockProyecto"), Is.EqualTo(1.25m));
        await VerificarObjetosRetirados(db);
    }

    [Test]
    public async Task Limpieza_BaseInicializada_ConservaDatosYBackups_YPermiteTodosLosMovimientos()
    {
        DatabaseUpgradeResult repeatables = SincronizarRepeatables();
        Assert.That(repeatables.Successful, Is.True, repeatables.Error?.ToString());
        DatabaseUpgradeResult historicos = DeployChanges.To.SqlDatabase(_cadena)
            .WithScriptsEmbeddedInAssembly(typeof(StockProyectoMigracionTests).Assembly,
                nombre => nombre.EndsWith("StockProyecto_PreLimpieza.sql", StringComparison.Ordinal))
            .JournalTo(new NullJournal()).LogToConsole().Build().PerformUpgrade();
        Assert.That(historicos.Successful, Is.True, historicos.Error?.ToString());

        await using SqlConnection db = new(_cadena);
        await PrepararMaestros(db);
        await db.ExecuteAsync("""
            INSERT INTO almacen.KardexEntrada (IdMaterial, IdProyecto, Fecha, Cantidad)
            VALUES (1, 1, '20261022', 10.25);
            INSERT INTO almacen.KardexStock (IdMaterial, TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento)
            VALUES (1, 10.25, 0, 10.25, '20261022');
            DECLARE @Distribucion almacen.TVP_StockProyectoCantidad;
            INSERT INTO @Distribucion VALUES (1, 1, 6.15), (1, 2, 4.10);
            EXEC almacen.usp_StockProyecto_Inicializar @Distribucion;
            DECLARE @Items almacen.TVP_TransferenciaProyectoItem;
            INSERT INTO @Items VALUES (1, 1.10);
            EXEC almacen.usp_TransferenciaProyecto_Registrar 1, 2, '20261022', N'Antes de limpieza', @Items;
            SELECT * INTO almacen.KardexEntrada_BKP_20261029_PreReinicioStockProyecto FROM almacen.KardexEntrada;
            SELECT * INTO almacen.KardexSalida_BKP_20261029_PreReinicioStockProyecto FROM almacen.KardexSalida;
            SELECT * INTO almacen.KardexSalidaDetalle_BKP_20261029_PreReinicioStockProyecto FROM almacen.KardexSalidaDetalle;
            SELECT * INTO almacen.KardexStock_BKP_20261029_PreReinicioStockProyecto FROM almacen.KardexStock;
            """);
        Assert.That(await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.StockProyectoInicializacion WHERE FechaActivacion IS NOT NULL"), Is.EqualTo(1));
        string antes = await CapturarInventario(db);
        Assert.That(await db.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.sql_expression_dependencies
            WHERE referenced_id = OBJECT_ID('almacen.usp_StockProyecto_ExigirInicializacion');
            """), Is.GreaterThanOrEqualTo(2), "La base anterior incluye dependencias directas e indirectas de la activación.");

        await AplicarLimpiezaYRepeatables();

        Assert.That(await CapturarInventario(db), Is.EqualTo(antes), "La limpieza debe preservar IDs, fechas, saldos, movimientos y backups.");
        await VerificarObjetosRetirados(db);
        Assert.That(await db.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('almacen')
              AND name IN ('KardexEntrada_BKP_20261029_PreReinicioStockProyecto',
                'KardexSalida_BKP_20261029_PreReinicioStockProyecto',
                'KardexSalidaDetalle_BKP_20261029_PreReinicioStockProyecto',
                'KardexStock_BKP_20261029_PreReinicioStockProyecto');
            """), Is.EqualTo(4));

        int entrada = await RegistrarEntrada(db, 2.25m);
        await db.QueryAsync("almacen.usp_KardexEntrada_Actualizar", new
        {
            IdKardexEntrada = entrada, IdMaterial = 1, IdProyecto = 1, Fecha = new DateTime(2026, 10, 22), Cantidad = 3.25m
        }, commandType: CommandType.StoredProcedure);
        await db.ExecuteAsync("almacen.usp_KardexEntrada_Eliminar", new { IdKardexEntrada = entrada }, commandType: CommandType.StoredProcedure);
        await db.ExecuteAsync("""
            DECLARE @Items almacen.TVP_KardexSalidaItem;
            INSERT INTO @Items (IdMaterial, Cantidad) VALUES (1, 1.15);
            EXEC almacen.usp_KardexSalida_Registrar @IdProyecto = 1, @Fecha = '20261022', @Solicitante = N'Limpieza', @Items = @Items;
            DECLARE @Salida INT = (SELECT MAX(IdKardexSalida) FROM almacen.KardexSalida);
            UPDATE @Items SET Cantidad = 2.15;
            EXEC almacen.usp_KardexSalida_Actualizar @IdKardexSalida = @Salida, @IdProyecto = 1,
                @Fecha = '20261022', @Solicitante = N'Limpieza', @Items = @Items;
            EXEC almacen.usp_KardexSalida_Eliminar @IdKardexSalida = @Salida;
            DECLARE @Transferencia almacen.TVP_TransferenciaProyectoItem;
            INSERT INTO @Transferencia VALUES (1, 1.25);
            EXEC almacen.usp_TransferenciaProyecto_Registrar 1, 2, '20261022', N'Después de limpieza', @Transferencia;
            """);
        Assert.That(await db.QuerySingleAsync<decimal>("SELECT Stock FROM almacen.StockProyecto WHERE IdProyecto = 1"), Is.EqualTo(3.80m));
        Assert.That(await db.QuerySingleAsync<decimal>("SELECT Stock FROM almacen.StockProyecto WHERE IdProyecto = 2"), Is.EqualTo(6.45m));
        Assert.That(await db.QuerySingleAsync<decimal>("SELECT Stock FROM almacen.KardexStock"), Is.EqualTo(10.25m));
        Assert.That(await db.QuerySingleAsync<decimal>("SELECT SUM(Stock) FROM almacen.StockProyecto"), Is.EqualTo(10.25m));
    }

    private async Task AplicarLimpiezaYRepeatables()
    {
        DatabaseUpgradeResult resultado = Migrar(nombre => nombre.EndsWith(MIGRACION, StringComparison.Ordinal));
        Assert.That(resultado.Successful, Is.True, resultado.Error?.ToString());

        resultado = SincronizarRepeatables();
        Assert.That(resultado.Successful, Is.True, resultado.Error?.ToString());

        resultado = Migrar(nombre => nombre.EndsWith(MIGRACION, StringComparison.Ordinal));
        Assert.That(resultado.Successful, Is.True, resultado.Error?.ToString());
        Assert.That(resultado.Scripts, Is.Empty, "DbUp no debe repetir la migración versionada.");

        resultado = SincronizarRepeatables();
        Assert.That(resultado.Successful, Is.True, resultado.Error?.ToString());

        await using SqlConnection db = new(_cadena);
        Assert.That(await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ScriptName LIKE '%V2_8_2__%'"), Is.EqualTo(1));
    }

    private static async Task PrepararMaestros(SqlConnection db)
    {
        await db.ExecuteAsync("""
            SET IDENTITY_INSERT maestra.Proyecto ON;
            INSERT INTO maestra.Proyecto (IdProyecto, NombreProyecto, Descripcion, Activo, FechaCreacion, CotizacionGeneral)
            VALUES (1, N'Origen', N'Prueba', 1, GETDATE(), 0), (2, N'Destino', N'Prueba', 1, GETDATE(), 0);
            SET IDENTITY_INSERT maestra.Proyecto OFF;
            """);
    }

    private static async Task<int> RegistrarEntrada(SqlConnection db, decimal cantidad)
    {
        KardexEntrada entrada = await db.QuerySingleAsync<KardexEntrada>(
            "almacen.usp_KardexEntrada_Registrar", new
            {
                IdMaterial = 1, IdProyecto = 1, Fecha = new DateTime(2026, 10, 22), Cantidad = cantidad
            }, commandType: CommandType.StoredProcedure);
        return entrada.IdKardexEntrada;
    }

    private static async Task VerificarObjetosRetirados(SqlConnection db)
    {
        Assert.That(await db.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.objects
            WHERE object_id IN (OBJECT_ID('almacen.StockProyectoInicializacion'),
                OBJECT_ID('almacen.usp_StockProyecto_Inicializar'), OBJECT_ID('almacen.usp_StockProyecto_ExigirInicializacion'));
            """), Is.Zero);
        Assert.That(await db.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.sql_modules
            WHERE definition LIKE '%StockProyectoInicializacion%' OR definition LIKE '%usp_StockProyecto_Inicializar%'
               OR definition LIKE '%usp_StockProyecto_ExigirInicializacion%' OR definition LIKE '%STOCK_PROYECTO_NO_INICIALIZADO%';
            """), Is.Zero);
        Assert.That(await db.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.procedures
            WHERE object_id IN (OBJECT_ID('almacen.usp_StockProyecto_ValidarCambios'),
                OBJECT_ID('almacen.usp_StockProyecto_AplicarCambios'), OBJECT_ID('almacen.usp_StockProyecto_VerificarConsistencia'));
            """), Is.EqualTo(3));
    }

    private static async Task<string> CapturarInventario(SqlConnection db)
        => await db.QuerySingleAsync<string>("""
            SELECT
                (SELECT * FROM almacen.KardexEntrada ORDER BY IdKardexEntrada FOR JSON PATH) AS Entradas,
                (SELECT * FROM almacen.KardexSalida ORDER BY IdKardexSalida FOR JSON PATH) AS Salidas,
                (SELECT * FROM almacen.KardexSalidaDetalle ORDER BY IdKardexSalidaDetalle FOR JSON PATH) AS DetallesSalida,
                (SELECT * FROM almacen.KardexStock ORDER BY IdMaterial FOR JSON PATH) AS Global,
                (SELECT * FROM almacen.StockProyecto ORDER BY IdStockProyecto FOR JSON PATH) AS Distribucion,
                (SELECT * FROM almacen.TransferenciaProyecto ORDER BY IdTransferencia FOR JSON PATH) AS Transferencias,
                (SELECT * FROM almacen.TransferenciaProyectoDetalle ORDER BY IdTransferenciaProyectoDetalle FOR JSON PATH) AS DetallesTransferencia,
                (SELECT * FROM almacen.KardexEntrada_BKP_20261029_PreReinicioStockProyecto ORDER BY IdKardexEntrada FOR JSON PATH) AS BackupEntradas,
                (SELECT * FROM almacen.KardexSalida_BKP_20261029_PreReinicioStockProyecto ORDER BY IdKardexSalida FOR JSON PATH) AS BackupSalidas,
                (SELECT * FROM almacen.KardexSalidaDetalle_BKP_20261029_PreReinicioStockProyecto ORDER BY IdKardexSalidaDetalle FOR JSON PATH) AS BackupDetallesSalida,
                (SELECT * FROM almacen.KardexStock_BKP_20261029_PreReinicioStockProyecto ORDER BY IdMaterial FOR JSON PATH) AS BackupGlobal
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
            """);

    private DatabaseUpgradeResult Migrar(Func<string, bool> filtro)
        => DeployChanges.To.SqlDatabase(_cadena)
            .WithScriptsEmbeddedInAssembly(typeof(InventarioModule).Assembly, filtro)
            .LogToConsole().Build().PerformUpgrade();

    private DatabaseUpgradeResult SincronizarRepeatables()
        => DeployChanges.To.SqlDatabase(_cadena)
            .WithScriptsEmbeddedInAssembly(typeof(InventarioModule).Assembly,
                nombre => nombre.Contains(".Migrations.Repeatable."))
            .JournalTo(new NullJournal()).LogToConsole().Build().PerformUpgrade();
}
