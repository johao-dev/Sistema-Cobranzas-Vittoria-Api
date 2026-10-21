using Dapper;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Inventario;

/// <summary>Verifica el upgrade de datos existentes en una base desechable por caso.</summary>
[NonParallelizable]
public class InventarioMigracionTests
{
    private const string MIGRACION = "V2_7_8__Inventario_Especialidad_Desde_Material.sql";
    private string _baseDatos = null!;
    private string _connectionString = null!;

    private DatabaseUpgradeResult Upgrade(Func<string, bool> filtro)
        => DeployChanges.To.SqlDatabase(_connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(Cobranzas_Vittoria.Inventario.InventarioModule).Assembly, filtro)
            .LogToConsole().LogScriptOutput().Build().PerformUpgrade();

    [SetUp]
    public async Task CrearBaseAnteriorALaMigracion()
    {
        _baseDatos = "inventario_migracion_" + Guid.NewGuid().ToString("N");
        await using SqlConnection master = new(GlobalSetupFixture.DbContainer.GetConnectionString());
        await master.ExecuteAsync($"CREATE DATABASE [{_baseDatos}]");
        _connectionString = new SqlConnectionStringBuilder(GlobalSetupFixture.DbContainer.GetConnectionString())
        {
            InitialCatalog = _baseDatos
        }.ConnectionString;

        string[] anteriores =
        {
            "V1_0_0__Schemas.sql", "V1_0_1__Maestra_DDL.sql",
            "V1_2_0__Almacen_EntradasSalidas_DDL.sql", "V1_4_1__Almacen_Stock_Global.sql"
        };
        DatabaseUpgradeResult resultado = Upgrade(nombre => anteriores.Any(script => nombre.EndsWith(script, StringComparison.Ordinal)));
        Assert.That(resultado.Successful, Is.True, resultado.Error?.ToString());

        await using SqlConnection connection = new(_connectionString);
        await connection.ExecuteAsync("""
            INSERT INTO maestra.Especialidad (Nombre) VALUES ('Especialidad A'), ('Especialidad B');
            INSERT INTO maestra.Material (IdEspecialidad, Codigo, Descripcion, UnidadMedida)
            VALUES (1, 'M1', 'Material A', 'UND');
            INSERT INTO almacen.KardexEntrada (IdEspecialidad, IdMaterial, Fecha, Cantidad)
            VALUES (1, 1, '20261021', 10.25);
            INSERT INTO almacen.KardexSalida (IdEspecialidad, Fecha, Solicitante)
            VALUES (1, '20261021', 'Migración');
            INSERT INTO almacen.KardexSalidaDetalle (IdKardexSalida, IdMaterial, Cantidad)
            VALUES (1, 1, 1.15);
            INSERT INTO almacen.KardexStock
                (IdMaterial, IdEspecialidad, TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento)
            VALUES (1, 1, 10.25, 1.15, 9.10, '20261021');
            """);
    }

    private DatabaseUpgradeResult Migrar()
        => Upgrade(nombre => nombre.EndsWith(MIGRACION, StringComparison.Ordinal));

    [Test]
    public async Task DatosConciliados_ConservaIdsCantidadesYCentavos_RetiraColumnasYPermiteReintento()
    {
        DatabaseUpgradeResult resultado = Migrar();
        Assert.That(resultado.Successful, Is.True, resultado.Error?.ToString());
        await using SqlConnection connection = new(_connectionString);

        Assert.That(await connection.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.columns WHERE name = 'IdEspecialidad'
            AND object_id IN (OBJECT_ID('almacen.KardexEntrada'), OBJECT_ID('almacen.KardexSalida'), OBJECT_ID('almacen.KardexStock'));
            """), Is.Zero);
        Assert.That(await connection.QuerySingleAsync<decimal>("SELECT Stock FROM almacen.KardexStock WHERE IdKardexStock = 1"), Is.EqualTo(9.10m));
        Assert.That(await connection.QuerySingleAsync<decimal>("SELECT Cantidad FROM almacen.KardexEntrada WHERE IdKardexEntrada = 1"), Is.EqualTo(10.25m));
        Assert.That(await connection.QuerySingleAsync<decimal>("SELECT Cantidad FROM almacen.KardexSalidaDetalle WHERE IdKardexSalidaDetalle = 1"), Is.EqualTo(1.15m));

        SqlException? duplicado = Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync("""
            INSERT INTO almacen.KardexStock (IdMaterial, TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento)
            VALUES (1, 0, 0, 0, '20261021');
            """));
        Assert.That(duplicado!.Number, Is.EqualTo(2627));
        Assert.That(Migrar().Successful, Is.True, "DbUp no debe repetir una migración ya aplicada.");
    }

    [TestCase("KardexEntrada")]
    [TestCase("KardexSalida")]
    [TestCase("KardexStock")]
    public async Task EspecialidadInconsistente_DetieneSinPerderLaEstructuraNiLosDatos(string tabla)
    {
        await using SqlConnection connection = new(_connectionString);
        // El identificador solo procede de los casos constantes declarados arriba.
        await connection.ExecuteAsync($"UPDATE almacen.{tabla} SET IdEspecialidad = 2");
        await VerificarBloqueoAsync(51181);
    }

    [Test]
    public async Task StockDuplicado_DetieneSinConsolidarFilas()
    {
        await using SqlConnection connection = new(_connectionString);
        await connection.ExecuteAsync("""
            INSERT INTO almacen.KardexStock
                (IdMaterial, IdEspecialidad, TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento)
            VALUES (1, 2, 0, 0, 0, '20261021');
            """);
        await VerificarBloqueoAsync(51180);
        Assert.That(await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.KardexStock"), Is.EqualTo(2));
    }

    [Test]
    public async Task AcumuladosInconsistentes_DetieneSinRecalcularNiBorrar()
    {
        await using SqlConnection connection = new(_connectionString);
        await connection.ExecuteAsync("UPDATE almacen.KardexStock SET Stock = 8.10");
        await VerificarBloqueoAsync(51182);
        Assert.That(await connection.QuerySingleAsync<decimal>("SELECT Stock FROM almacen.KardexStock"), Is.EqualTo(8.10m));

        await connection.ExecuteAsync("UPDATE almacen.KardexStock SET Stock = 9.10");
        Assert.That(Migrar().Successful, Is.True, "Se admite el reintento después de conciliar los datos.");
    }

    [Test]
    public async Task SalidaSinDetalles_DetieneSinEliminarCabeceras()
    {
        await using SqlConnection connection = new(_connectionString);
        await connection.ExecuteAsync("INSERT INTO almacen.KardexSalida (IdEspecialidad, Fecha, Solicitante) VALUES (1, '20261021', 'Sin detalles')");
        await VerificarBloqueoAsync(51183);
        Assert.That(await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.KardexSalida"), Is.EqualTo(2));
    }

    private async Task VerificarBloqueoAsync(int numero)
    {
        DatabaseUpgradeResult resultado = Migrar();
        Assert.That(resultado.Successful, Is.False);
        Assert.That(resultado.Error, Is.InstanceOf<SqlException>(), resultado.Error?.ToString());
        Assert.That(((SqlException)resultado.Error).Number, Is.EqualTo(numero));

        await using SqlConnection connection = new(_connectionString);
        Assert.That(await connection.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.columns WHERE name = 'IdEspecialidad'
            AND object_id IN (OBJECT_ID('almacen.KardexEntrada'), OBJECT_ID('almacen.KardexSalida'), OBJECT_ID('almacen.KardexStock'));
            """), Is.EqualTo(3));
        Assert.That(await connection.QuerySingleAsync<decimal>("SELECT Cantidad FROM almacen.KardexEntrada WHERE IdKardexEntrada = 1"), Is.EqualTo(10.25m));
        Assert.That(await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ScriptName LIKE '%V2_7_8__%'"), Is.Zero);
    }

    [TearDown]
    public async Task EliminarBaseDesechable()
    {
        if (_baseDatos is null) return;
        SqlConnection.ClearAllPools();
        await using SqlConnection master = new(GlobalSetupFixture.DbContainer.GetConnectionString());
        await master.ExecuteAsync($"ALTER DATABASE [{_baseDatos}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_baseDatos}]");
    }
}
