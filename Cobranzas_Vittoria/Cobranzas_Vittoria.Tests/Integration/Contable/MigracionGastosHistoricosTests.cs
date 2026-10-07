using Cobranzas_Vittoria.Entities;
using Dapper;
using DbUp;
using DbUp.Engine;
using DbUp.Helpers;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

/// <summary>
/// V2.6.3: los gastos de las pantallas antiguas sin partida presupuestal pasan a contable.GastoDirecto
/// bajo un presupuesto histórico inactivo, sin movimientos. Upgrade real en una DB desechable.
/// </summary>
[NonParallelizable]
public class MigracionGastosHistoricosTests
{
    private string _database = null!;
    private string _connectionString = null!;

    [SetUp]
    public async Task CrearBaseHastaV2_6_2()
    {
        _database = "historicos_" + Guid.NewGuid().ToString("N");
        await using var master = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await master.OpenAsync();
        await master.ExecuteAsync($"CREATE DATABASE [{_database}]");
        _connectionString = new SqlConnectionStringBuilder(GlobalSetupFixture.DbContainer.GetConnectionString())
            { InitialCatalog = _database }.ConnectionString;
        var result = Upgrade(name => name.Contains(".Migrations.Versioned.")
            && !name.Contains(".V2_6_3__") && !name.Contains(".V2_7_0__") && !name.Contains(".V2_7_1__"));
        Assert.That(result.Successful, Is.True, result.Error?.ToString());
    }

    [TearDown]
    public async Task EliminarBaseDesechable()
    {
        if (_database is null) return;
        SqlConnection.ClearAllPools();
        await using var master = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await master.OpenAsync();
        await master.ExecuteAsync($"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}];");
    }

    private DatabaseUpgradeResult Upgrade(Func<string, bool> filtro) => DeployChanges.To
        .SqlDatabase(_connectionString)
        .WithScriptsEmbeddedInAssembly(typeof(OrdenCompra).Assembly, filtro)
        .LogToConsole().Build().PerformUpgrade();

    private DatabaseUpgradeResult SincronizarRepeatables() => DeployChanges.To
        .SqlDatabase(_connectionString)
        .WithScriptsEmbeddedInAssembly(typeof(OrdenCompra).Assembly,
            name => name.Contains(".Migrations.Repeatable."))
        .JournalTo(new NullJournal())
        .LogToConsole().Build().PerformUpgrade();

    [Test]
    public async Task Upgrade_ConservaGastosYPartidasHistoricas_YTerminaConSchemaFinal()
    {
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();
        await cn.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.EstadoPresupuesto (Codigo, Nombre)
            SELECT v.Codigo, v.Codigo FROM (VALUES ('BORRADOR'), ('APROBADO'), ('HISTORICO'), ('ANULADO')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.EstadoPresupuesto e WHERE e.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo, Nombre)
            SELECT v.Codigo, v.Codigo FROM (VALUES ('PROYECTO'), ('ADMINISTRACION')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoCentroCosto t WHERE t.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre)
            SELECT v.Codigo, v.Codigo FROM (VALUES ('INDIRECTOS'), ('ADMINISTRATIVOS')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoPartida t WHERE t.Codigo = v.Codigo);
            IF NOT EXISTS (SELECT 1 FROM maestra.Moneda WHERE Codigo = 'USD')
                INSERT INTO maestra.Moneda (Codigo, Nombre, Simbolo) VALUES ('USD', N'Dólar', '$');
            """);
        var proyecto = await cn.QuerySingleAsync<int>(
            "INSERT INTO maestra.Proyecto (NombreProyecto) VALUES (N'Residencial Histórico'); SELECT CONVERT(INT, SCOPE_IDENTITY());");
        var categoria = await cn.QuerySingleAsync<int>(
            "INSERT INTO maestra.CategoriaGasto (Nombre) VALUES (N'MOVILIDAD'); SELECT CONVERT(INT, SCOPE_IDENTITY());");
        await cn.ExecuteAsync("""
            INSERT INTO contable.GastoProyecto (TipoModulo, IdProyecto, Fecha, Concepto, Moneda, MontoSoles, MontoDolares,
                TipoCambio, Estado, Activo, FechaCreacion)
            VALUES ('Terreno', @proyecto, '2025-03-10', N'Compra de lote', 'PEN', 150000, 0, 3.7, 'Activo', 1, GETDATE()),
                   ('GastosMunicipales', @proyecto, '2025-04-02', N'Licencia', 'USD', 0, 2000, 3.7, 'Inactivo', 0, GETDATE()),
                   ('Marketing', @proyecto, '2025-05-01', N'Sin monto', 'PEN', 0, 0, 3.7, 'Activo', 1, GETDATE()),
                   ('Marketing', @proyecto, '2025-05-10', N'Campaña histórica', 'PEN', 900, 0, 3.7, 'Activo', 1, GETDATE()),
                   ('OtrosGastos', @proyecto, '2025-05-20', N'Otro gasto histórico', 'PEN', 450, 0, 3.7, 'Activo', 1, GETDATE());
            INSERT INTO contable.GastoAdministrativo (IdCategoriaGasto, Fecha, Monto, Descripcion, Moneda, Activo, FechaCreacion)
            VALUES (@categoria, '2025-06-01', 850.50, N'Taxis', 'PEN', 1, GETDATE());
            INSERT INTO contable.GastoAdministrativoDocumento
                (IdGastoAdministrativo, TipoDocumento, NombreArchivo, RutaArchivo, Extension)
            SELECT TOP (1) IdGastoAdministrativo, 'Factura', N'historica.pdf', N'/legacy/historica.pdf', N'.pdf'
            FROM contable.GastoAdministrativo WHERE IdCategoriaGasto=@categoria ORDER BY IdGastoAdministrativo DESC;
            """, new { proyecto, categoria });

        var result = Upgrade(name => name.Contains(".V2_6_3__"));
        Assert.That(result.Successful, Is.True, result.Error?.ToString());

        var retiro = Upgrade(name => name.Contains(".V2_7_0__"));
        Assert.That(retiro.Successful, Is.True, retiro.Error?.ToString());

        var detalleExistente = await cn.QuerySingleAsync<int>(
            "SELECT TOP (1) IdPresupuestoDetalle FROM contable.GastoDirecto ORDER BY IdGastoDirecto");
        var monedaExistente = await cn.QuerySingleAsync<int>(
            "SELECT TOP (1) IdMoneda FROM contable.GastoDirecto ORDER BY IdGastoDirecto");
        await cn.ExecuteAsync("""
            INSERT INTO contable.GastoDirecto
                (IdPresupuestoDetalle, IdMoneda, Fecha, Concepto, Monto, Estado)
            VALUES (@detalle, @moneda, '2025-07-01', N'Gasto sin fuente legacy', 77.25, 'REGISTRADO');
            """, new { detalle = detalleExistente, moneda = monedaExistente });

        var antes = (await cn.QueryAsync<(int Id, decimal Monto, int Documentos)>("""
            SELECT gd.IdGastoDirecto, gd.Monto, COUNT(d.IdGastoDirectoDocumento)
            FROM contable.GastoDirecto gd
            LEFT JOIN contable.GastoDirectoDocumento d ON d.IdGastoDirecto=gd.IdGastoDirecto
            GROUP BY gd.IdGastoDirecto, gd.Monto ORDER BY gd.IdGastoDirecto
            """)).ToList();
        var movimientosAntes = await cn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM ControlPresupuestario.MovimientoPresupuestal");

        var categoriaGasto = Upgrade(name => name.Contains(".V2_7_1__"));
        Assert.That(categoriaGasto.Successful, Is.True, categoriaGasto.Error?.ToString());
        var repeatablesFinales = SincronizarRepeatables();
        Assert.That(repeatablesFinales.Successful, Is.True, repeatablesFinales.Error?.ToString());

        var gastos = (await cn.QueryAsync<(string Concepto, decimal Monto, string Estado, string Moneda, string Partida, string Presupuesto, bool Activo, string Centro, int? IdCategoria, string? Categoria)>("""
            SELECT gd.Concepto, gd.Monto, gd.Estado, m.Codigo, cp.Codigo, p.Codigo, p.Activo, cc.Codigo,
                   gd.IdCategoriaGasto, cg.Codigo
            FROM contable.GastoDirecto gd
            LEFT JOIN maestra.CategoriaGasto cg ON cg.IdCategoriaGasto=gd.IdCategoriaGasto
            JOIN maestra.Moneda m ON m.IdMoneda = gd.IdMoneda
            JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoDetalle = gd.IdPresupuestoDetalle
            JOIN ControlPresupuestario.CatalogoPartida cp ON cp.IdCatalogoPartida = pd.IdCatalogoPartida
            JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = pv.IdPresupuesto
            JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
            ORDER BY gd.Fecha
            """)).ToList();

        Assert.That(gastos, Has.Count.EqualTo(6), "El gasto con monto 0 no se migra; se conserva el gasto directo previo.");
        Assert.That((gastos[0].Concepto, gastos[0].Monto, gastos[0].Partida, gastos[0].IdCategoria),
            Is.EqualTo(("Compra de lote", 150000m, "HIST.TERRENO", (int?)null)),
            "TipoModulo Terreno no distingue TERRENO/ANTEPROYECTO/PROYECTO.");
        Assert.That((gastos[1].Monto, gastos[1].Estado, gastos[1].Moneda, gastos[1].Partida), Is.EqualTo((2000m, "ANULADO", "USD", "HIST.MUNICIPAL")));
        Assert.That(gastos[1].Categoria, Is.EqualTo("MUNICIPAL"));
        Assert.That(gastos[1].Presupuesto, Is.Not.EqualTo(gastos[0].Presupuesto), "Un presupuesto histórico por moneda.");
        Assert.That(gastos[2].Categoria, Is.EqualTo("MARKETING_VENTAS"));
        Assert.That(gastos[3].Categoria, Is.EqualTo("OTROS"));
        Assert.That((gastos[4].Concepto, gastos[4].Partida, gastos[4].Centro), Is.EqualTo(("MOVILIDAD", "HIST.ADMINISTRATIVO", "CC-ADM-HIST")));
        Assert.That(gastos[4].IdCategoria, Is.EqualTo(categoria), "GastoAdministrativo conserva la categoría legacy exacta.");
        Assert.That(gastos[5].IdCategoria, Is.Null, "Un GastoDirecto sin fuente legacy no recibe una categoría inventada.");
        Assert.That(gastos.All(g => !g.Activo), Is.True, "Los presupuestos históricos quedan inactivos.");
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM ControlPresupuestario.MovimientoPresupuestal"),
            Is.EqualTo(movimientosAntes));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM contable.GastoDirectoLegacyMap"), Is.EqualTo(5));
        var despues = (await cn.QueryAsync<(int Id, decimal Monto, int Documentos)>("""
            SELECT gd.IdGastoDirecto, gd.Monto, COUNT(d.IdGastoDirectoDocumento)
            FROM contable.GastoDirecto gd
            LEFT JOIN contable.GastoDirectoDocumento d ON d.IdGastoDirecto=gd.IdGastoDirecto
            GROUP BY gd.IdGastoDirecto, gd.Monto ORDER BY gd.IdGastoDirecto
            """)).ToList();
        Assert.That(despues, Is.EqualTo(antes), "V2.7.1 preserva IDs, montos y documentos.");
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT is_nullable FROM sys.columns
            WHERE object_id=OBJECT_ID('contable.GastoDirecto') AND name='IdCategoriaGasto'
            """), Is.EqualTo(1), "La historia ambigua obliga a conservar nulabilidad transitoria.");
        Assert.That(await cn.QuerySingleAsync<string>("""
            SELECT ep.Codigo FROM ControlPresupuestario.PresupuestoVersion pv
            JOIN ControlPresupuestario.EstadoPresupuesto ep ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = pv.IdPresupuesto
            WHERE p.Codigo LIKE 'HIST-%' GROUP BY ep.Codigo
            """), Is.EqualTo("APROBADO"));
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.tables
            WHERE object_id IN (OBJECT_ID('ControlPresupuestario.SeccionGasto'),
                OBJECT_ID('ControlPresupuestario.SeccionGastoTipoCentroCosto'),
                OBJECT_ID('ControlPresupuestario.SeccionGastoCategoriaGasto'))
            """), Is.Zero);
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.columns
            WHERE object_id = OBJECT_ID('ControlPresupuestario.CatalogoPartida') AND name = 'IdSeccionGasto'
            """), Is.Zero);
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.table_types tt
            JOIN sys.columns c ON c.object_id = tt.type_table_object_id
            WHERE SCHEMA_NAME(tt.schema_id) = 'ControlPresupuestario'
              AND tt.name = 'TVP_CatalogoPartida' AND c.name = 'IdSeccionGasto'
            """), Is.Zero);
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.foreign_keys
            WHERE parent_object_id=OBJECT_ID('contable.GastoDirecto')
              AND name='FK_GastoDirecto_CategoriaGasto'
            """), Is.EqualTo(1));
    }

    [Test]
    public void SinGastosAntiguos_NoCreaNada()
    {
        Assert.That(Upgrade(name => name.Contains(".V2_6_3__")).Successful, Is.True);
        using var cn = new SqlConnection(_connectionString);
        Assert.That(cn.QuerySingle<int>("SELECT COUNT(*) FROM ControlPresupuestario.CatalogoPartida WHERE Codigo LIKE 'HIST%'"), Is.Zero);
    }
}
