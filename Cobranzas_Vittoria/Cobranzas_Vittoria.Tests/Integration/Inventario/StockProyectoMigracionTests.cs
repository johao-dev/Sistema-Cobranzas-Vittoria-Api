using Cobranzas_Vittoria.Inventario;
using Dapper;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Inventario;

[NonParallelizable]
public class StockProyectoMigracionTests
{
    [Test]
    public async Task MigrarGlobalExistente_CreaEstructuraSinDistribuirNiActivar_YPermiteReintento()
    {
        string baseDatos = "stock_proyecto_migracion_" + Guid.NewGuid().ToString("N");
        await using SqlConnection master = new(GlobalSetupFixture.DbContainer.GetConnectionString());
        await master.ExecuteAsync($"CREATE DATABASE [{baseDatos}]");
        string cadena = new SqlConnectionStringBuilder(GlobalSetupFixture.DbContainer.GetConnectionString())
        {
            InitialCatalog = baseDatos
        }.ConnectionString;
        try
        {
            string[] anteriores =
            {
                "V1_0_0__Schemas.sql", "V1_0_1__Maestra_DDL.sql", "V1_2_0__Almacen_EntradasSalidas_DDL.sql",
                "V1_4_1__Almacen_Stock_Global.sql", "V2_7_8__Inventario_Especialidad_Desde_Material.sql"
            };
            DatabaseUpgradeResult resultado = Migrar(cadena, nombre => anteriores.Any(script => nombre.EndsWith(script, StringComparison.Ordinal)));
            Assert.That(resultado.Successful, Is.True, resultado.Error?.ToString());

            await using SqlConnection db = new(cadena);
            await db.ExecuteAsync("""
                INSERT INTO maestra.Especialidad (Nombre) VALUES ('Prueba');
                INSERT INTO maestra.Material (IdEspecialidad, Codigo, Descripcion, UnidadMedida)
                VALUES (1, 'P1', 'Prueba de migración', 'UND');
                INSERT INTO maestra.Proyecto (NombreProyecto, Descripcion, Activo, FechaCreacion, CotizacionGeneral)
                VALUES ('Proyecto histórico', 'Prueba', 1, GETDATE(), 0);
                INSERT INTO almacen.KardexEntrada (IdMaterial, IdProyecto, Fecha, Cantidad)
                VALUES (1, 1, '20261022', 10.25);
                INSERT INTO almacen.KardexStock (IdMaterial, TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento)
                VALUES (1, 10.25, 0, 10.25, '20261022');
                """);

            resultado = Migrar(cadena, nombre => nombre.Contains(".Versioned.V2_8_"));
            Assert.That(resultado.Successful, Is.True, resultado.Error?.ToString());
            Assert.That(await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.StockProyecto"), Is.Zero);
            Assert.That(await db.QuerySingleAsync<decimal>("SELECT Stock FROM almacen.KardexStock"), Is.EqualTo(10.25m));
            Assert.That(await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.StockProyectoInicializacion WHERE FechaActivacion IS NULL"), Is.EqualTo(1));
            Assert.That(await db.QuerySingleAsync<string>("""
                SELECT c.name FROM sys.index_columns ic
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                WHERE i.name = 'PK_TransferenciaProyectoDetalle';
                """), Is.EqualTo("IdTransferenciaProyectoDetalle"));
            Assert.That(Migrar(cadena, nombre => nombre.Contains(".Versioned.V2_8_")).Successful, Is.True);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await master.ExecuteAsync($"ALTER DATABASE [{baseDatos}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{baseDatos}]");
        }
    }

    private static DatabaseUpgradeResult Migrar(string cadena, Func<string, bool> filtro)
        => DeployChanges.To.SqlDatabase(cadena)
            .WithScriptsEmbeddedInAssembly(typeof(InventarioModule).Assembly, filtro)
            .LogToConsole().Build().PerformUpgrade();
}
