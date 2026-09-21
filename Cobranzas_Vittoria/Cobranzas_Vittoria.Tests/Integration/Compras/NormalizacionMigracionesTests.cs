using Cobranzas_Vittoria.Entities;
using Dapper;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

/// <summary>Upgrade real desde V2.0.9 con datos históricos, en una DB desechable por caso.</summary>
[NonParallelizable]
public class NormalizacionMigracionesTests
{
    private string _database = null!;
    private string _connectionString = null!;

    [SetUp]
    public async Task CrearBaseLegacy()
    {
        _database = "normalizacion_" + Guid.NewGuid().ToString("N");
        await using var master = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await master.OpenAsync();
        await master.ExecuteAsync($"CREATE DATABASE [{_database}]");
        _connectionString = new SqlConnectionStringBuilder(GlobalSetupFixture.DbContainer.GetConnectionString())
            { InitialCatalog = _database }.ConnectionString;
        var result = Upgrade(name => name.Contains(".Migrations.Versioned.")
            && !name.Contains(".V2_1_") && !name.Contains(".V2_2_"));
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

    private DatabaseUpgradeResult Upgrade(Func<string, bool> filter) => DeployChanges.To
        .SqlDatabase(_connectionString)
        .WithScriptsEmbeddedInAssembly(typeof(OrdenCompra).Assembly, filter)
        .LogToConsole().LogScriptOutput().Build().PerformUpgrade();

    private DatabaseUpgradeResult NormalizarHasta(int version = 8) => Upgrade(name =>
        name.Contains(".Migrations.Versioned.V2_1_") &&
        int.Parse(name.Split("V2_1_")[1].Split("__")[0]) <= version);

    private DatabaseUpgradeResult IntegrarEconomia() => Upgrade(name =>
        name.Contains(".Migrations.Versioned.V2_2_"));

    private async Task<SqlConnection> Conexion()
    {
        var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();
        return cn;
    }

    private static void Bloqueado(DatabaseUpgradeResult result, int error)
    {
        Assert.That(result.Successful, Is.False);
        Assert.That(result.Error, Is.InstanceOf<SqlException>(), result.Error?.ToString());
        Assert.That(((SqlException)result.Error).Number, Is.EqualTo(error));
    }

    private async Task<(int Req,int Oc)> CrearOcLegacy()
    {
        await using var cn = await Conexion();
        var req = await cn.QuerySingleAsync<int>("""
            INSERT INTO compras.Requerimiento
                (NumeroRequerimiento,FechaRequerimiento,IdEspecialidad,IdProyecto,IdUsuarioSolicitante,Estado)
            VALUES ('LEGACY',GETDATE(),2,10,2,'Registrado'); SELECT CONVERT(INT,SCOPE_IDENTITY());
            """);
        var oc = await cn.QuerySingleAsync<int>("""
            INSERT INTO compras.OrdenCompra
                (NumeroOrdenCompra,IdRequerimiento,IdProveedor,IdProyecto,FechaOrdenCompra,Estado)
            VALUES ('LEGACY',@req,2,10,GETDATE(),'Registrada'); SELECT CONVERT(INT,SCOPE_IDENTITY());
            """,new{req});
        return (req,oc);
    }

    [Test]
    public async Task Moneda_ConViewsYSpsRealesPreexistentes_ActualizaSinRecrearlos()
    {
        // Reproducir el estado real de una aplicación que ya ejecutó los repeatables:
        // las definiciones sys.sql_modules incluyen sus comentarios de cabecera.
        var assembly = typeof(OrdenCompra).Assembly;
        var scripts = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Migrations.Repeatable.") && name.Contains("R__ControlPresupuestario_"))
            // Estos repeatables requieren la columna CentroCosto.IdProyecto de V2.2.0;
            // no existían en el estado V2.0.9 que este caso reproduce.
            .Where(name => !name.Contains("_Lecturas_SPs.sql") && !name.Contains("_Maestros_SPs.sql"))
            .OrderBy(name => name)
            .Select(name =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                return new SqlScript(name, reader.ReadToEnd().Replace("maestra.Moneda", "ControlPresupuestario.Moneda"));
            }).ToArray();
        var legacy = DeployChanges.To.SqlDatabase(_connectionString).WithScripts(scripts)
            .LogToConsole().Build().PerformUpgrade();
        Assert.That(legacy.Successful, Is.True, legacy.Error?.ToString());
        await using var cn = await Conexion();
        var viewId = await cn.QuerySingleAsync<int>("SELECT OBJECT_ID('ControlPresupuestario.vw_ControlPresupuestarioVigente')");
        await cn.ExecuteAsync("CREATE USER lector_vigente WITHOUT LOGIN; GRANT SELECT ON OBJECT::ControlPresupuestario.vw_ControlPresupuestarioVigente TO lector_vigente;");
        var result = NormalizarHasta();
        Assert.That(result.Successful, Is.True, result.Error?.ToString());
        Assert.That(await cn.QuerySingleAsync<int>("SELECT OBJECT_ID('ControlPresupuestario.vw_ControlPresupuestarioVigente')"), Is.EqualTo(viewId));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.sql_modules WHERE definition LIKE '%ControlPresupuestario.Moneda%'"), Is.Zero);
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.database_permissions WHERE class=1
                AND major_id=OBJECT_ID('ControlPresupuestario.vw_ControlPresupuestarioVigente')
                AND grantee_principal_id=USER_ID('lector_vigente') AND permission_name='SELECT' AND state='G';
            """), Is.EqualTo(1));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM ControlPresupuestario.vw_ControlPresupuestarioVigente"), Is.Zero);
        Assert.That(NormalizarHasta().Successful, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CatalogoYPartida_PreservanIdsDatosYAsociaciones_RecreanTvpYDependencias(bool sinDetalles)
    {
        await using var cn = await Conexion();
        await cn.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.Moneda (Codigo,Nombre,Simbolo,Activo) VALUES ('CHF','Franco','Fr',0);
            INSERT INTO ControlPresupuestario.CentroCosto (Codigo,Nombre,IdTipoCentroCosto)
                SELECT 'TEST','Test',IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo='PROYECTO';
            INSERT INTO ControlPresupuestario.Presupuesto (IdCentroCosto,IdMoneda,Codigo,Nombre)
                SELECT c.IdCentroCosto,m.IdMoneda,'TEST','Test' FROM ControlPresupuestario.CentroCosto c
                CROSS JOIN ControlPresupuestario.Moneda m WHERE c.Codigo='TEST' AND m.Codigo='USD';
            INSERT INTO ControlPresupuestario.PresupuestoVersion (IdPresupuesto,IdEstadoPresupuesto,NumeroVersion)
                SELECT p.IdPresupuesto,e.IdEstadoPresupuesto,1 FROM ControlPresupuestario.Presupuesto p
                CROSS JOIN ControlPresupuestario.EstadoPresupuesto e WHERE p.Codigo='TEST' AND e.Codigo='BORRADOR';
            INSERT INTO ControlPresupuestario.CatalogoPartida (Codigo,Nombre,IdTipoPartida,Nivel)
                SELECT 'TEST','Test',IdTipoPartida,1 FROM ControlPresupuestario.TipoPartida WHERE Codigo='MATERIALES';
            INSERT INTO ControlPresupuestario.PresupuestoDetalle (IdPresupuestoVersion,IdCatalogoPartida,MontoPresupuestado)
                SELECT v.IdPresupuestoVersion,c.IdCatalogoPartida,100 FROM ControlPresupuestario.PresupuestoVersion v
                CROSS JOIN ControlPresupuestario.CatalogoPartida c WHERE c.Codigo='TEST';
            INSERT INTO compras.Requerimiento
                (NumeroRequerimiento,FechaRequerimiento,IdEspecialidad,IdProyecto,IdUsuarioSolicitante,Estado,IdPresupuestoDetalle)
                SELECT 'LEGACY',GETDATE(),2,10,2,'Registrado',IdPresupuestoDetalle FROM ControlPresupuestario.PresupuestoDetalle;
            INSERT INTO compras.RequerimientoDetalle (IdRequerimiento,IdMaterial,Cantidad)
                SELECT r.IdRequerimiento,v.Material,1 FROM compras.Requerimiento r CROSS JOIN (VALUES (2),(6)) v(Material);
            """);
        await cn.ExecuteAsync("CREATE OR ALTER VIEW ControlPresupuestario.vw_MonedaTest AS SELECT IdMoneda,Codigo FROM ControlPresupuestario.Moneda;");
        await cn.ExecuteAsync("CREATE OR ALTER PROCEDURE compras.usp_TvpTest @Items compras.TVP_RequerimientoDetalle READONLY AS SELECT COUNT(*) AS Lineas FROM @Items;");
        await cn.ExecuteAsync("""
            /* Cabecera con CREATE PROCEDURE dentro de un comentario.
               /* Comentario anidado con ALTER PROCEDURE. */
            */
            -- CREATE OR ALTER también puede aparecer en un comentario de línea.
            ALTER PROCEDURE compras.usp_TvpTest @Items compras.TVP_RequerimientoDetalle READONLY
            AS
            BEGIN
                DECLARE @texto NVARCHAR(30)=N'CREATE OR ALTER';
                SELECT COUNT(*) AS Lineas FROM @Items;
            END;
            """);
        await cn.ExecuteAsync("CREATE USER lector_tvp WITHOUT LOGIN; GRANT EXECUTE ON OBJECT::compras.usp_TvpTest TO lector_tvp; GRANT REFERENCES ON TYPE::compras.TVP_RequerimientoDetalle TO lector_tvp;");
        await cn.ExecuteAsync("CREATE OR ALTER VIEW compras.vw_Compra_ListadoConEspecialidad AS SELECT IdRequerimiento,IdEspecialidad FROM compras.Requerimiento;");
        var monedas=(await cn.QueryAsync<Moneda>("SELECT IdMoneda,Codigo,Nombre,Simbolo,Activo FROM ControlPresupuestario.Moneda ORDER BY IdMoneda")).ToArray();
        var partida=await cn.QuerySingleAsync<int>("SELECT IdPresupuestoDetalle FROM compras.Requerimiento");
        var monedaPresupuesto=await cn.QuerySingleAsync<int>("SELECT IdMoneda FROM ControlPresupuestario.Presupuesto");
        if (sinDetalles)
        {
            await cn.ExecuteAsync("DELETE FROM compras.RequerimientoDetalle");
            Bloqueado(NormalizarHasta(),51310);
            Assert.That(await cn.QuerySingleAsync<int>("SELECT IdPresupuestoDetalle FROM compras.Requerimiento"),Is.EqualTo(partida));
            Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('compras.RequerimientoDetalle') AND name='IdPresupuestoDetalle'"),Is.Zero);
            return;
        }
        var result=NormalizarHasta();
        Assert.That(result.Successful,Is.True,result.Error?.ToString());
        Assert.That((await cn.QueryAsync<Moneda>("SELECT IdMoneda,Codigo,Nombre,Simbolo,Activo FROM maestra.Moneda ORDER BY IdMoneda")).ToArray(),Is.EqualTo(monedas));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT IdMoneda FROM ControlPresupuestario.Presupuesto"),Is.EqualTo(monedaPresupuesto));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM compras.RequerimientoDetalle WHERE IdPresupuestoDetalle=@partida",new{partida}),Is.EqualTo(2));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM ControlPresupuestario.vw_MonedaTest"),Is.EqualTo(monedas.Length));
        Assert.That(await cn.QuerySingleAsync<int>("""
            DECLARE @items compras.TVP_RequerimientoDetalle;
            INSERT INTO @items VALUES (2,1,NULL,@partida);
            EXEC compras.usp_TvpTest @items;
            """,new{partida}),Is.EqualTo(1));
        Assert.That(await cn.QuerySingleAsync<string>("SELECT OBJECT_DEFINITION(OBJECT_ID('compras.usp_TvpTest'))"),
            Does.Contain("N'CREATE OR ALTER'"), "La normalización del encabezado no debe modificar literales del cuerpo del SP.");
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.database_permissions WHERE grantee_principal_id=USER_ID('lector_tvp')
                AND state='G' AND ((class=1 AND major_id=OBJECT_ID('compras.usp_TvpTest') AND permission_name='EXECUTE')
                    OR (class=6 AND major_id=TYPE_ID('compras.TVP_RequerimientoDetalle') AND permission_name='REFERENCES'));
            """),Is.EqualTo(2));
        Assert.That(NormalizarHasta().Successful,Is.True,"Reintento DbUp no debe volver a ejecutar migraciones aplicadas.");
    }

    [Test]
    public async Task MonedaHistorica_NoSeInventa_PermiteBackfillDocumentadoYReintento()
    {
        var (_,oc)=await CrearOcLegacy();
        var result=NormalizarHasta();
        Bloqueado(result,51340);
        await using var cn=await Conexion();
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM compras.OrdenCompra WHERE IdMoneda IS NULL"),Is.EqualTo(1));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ScriptName LIKE '%V2_1_8__%'"),Is.Zero);
        // Evidencia simulada explícita: este caso histórico era USD. Nunca usar PEN por defecto.
        await cn.ExecuteAsync("UPDATE compras.OrdenCompra SET IdMoneda=(SELECT IdMoneda FROM maestra.Moneda WHERE Codigo='USD') WHERE IdOrdenCompra=@oc",new{oc});
        result=NormalizarHasta();
        Assert.That(result.Successful,Is.True,result.Error?.ToString());
        Assert.That(await cn.QuerySingleAsync<string>("SELECT m.Codigo FROM compras.OrdenCompra oc JOIN maestra.Moneda m ON m.IdMoneda=oc.IdMoneda"),Is.EqualTo("USD"));
        Assert.That(await cn.QuerySingleAsync<bool>("SELECT is_nullable FROM sys.columns WHERE object_id=OBJECT_ID('compras.OrdenCompra') AND name='IdMoneda'"),Is.False);
    }

    [Test]
    public async Task DuplicadosOc_NoEliminaLineasNiCreaConstraint()
    {
        var (_,oc)=await CrearOcLegacy();
        await using var cn=await Conexion();
        await cn.ExecuteAsync("""
            INSERT INTO compras.OrdenCompraDetalle (IdOrdenCompra,IdMaterial,IdProveedor,Cantidad,PrecioUnitario)
            VALUES (@oc,2,2,1,1),(@oc,2,2,2,1);
            """,new{oc});
        Bloqueado(NormalizarHasta(6),51330);
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM compras.OrdenCompraDetalle"),Is.EqualTo(2));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.key_constraints WHERE name='UQ_OrdenCompraDetalle_OrdenCompra_Material'"),Is.Zero);
    }

    [Test]
    public async Task DuplicadosCompra_NoEliminaLineasNiCreaConstraint()
    {
        var (_,oc)=await CrearOcLegacy();
        await using var cn=await Conexion();
        await cn.ExecuteAsync("""
            INSERT INTO compras.OrdenCompraDetalle (IdOrdenCompra,IdMaterial,IdProveedor,Cantidad,PrecioUnitario) VALUES (@oc,2,2,1,1);
            INSERT INTO compras.Compra (NumeroCompra,IdOrdenCompra,IdProveedor,FechaCompra) VALUES ('LEGACY',@oc,2,GETDATE());
            DECLARE @compra INT=SCOPE_IDENTITY();
            INSERT INTO compras.CompraDetalle (IdCompra,IdMaterial,Cantidad,PrecioUnitario) VALUES (@compra,2,1,1),(@compra,2,2,1);
            """,new{oc});
        Bloqueado(NormalizarHasta(7),51330);
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM compras.CompraDetalle"),Is.EqualTo(2));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.key_constraints WHERE name='UQ_CompraDetalle_Compra_Material'"),Is.Zero);
    }

    [Test]
    public async Task SubtotalFueraDeRango_NoConvierteNiEliminaColumnaCalculada()
    {
        var (_,oc)=await CrearOcLegacy();
        await using var cn=await Conexion();
        await cn.ExecuteAsync("""
            INSERT INTO compras.OrdenCompraDetalle (IdOrdenCompra,IdMaterial,IdProveedor,Cantidad,PrecioUnitario)
            VALUES (@oc,2,2,9999999999999999.99,2);
            """,new{oc});
        Bloqueado(NormalizarHasta(5),51320);
        Assert.That(await cn.QuerySingleAsync<int>("SELECT precision FROM sys.columns WHERE object_id=OBJECT_ID('compras.OrdenCompraDetalle') AND name='Subtotal'"),Is.EqualTo(37));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM compras.OrdenCompraDetalle"),Is.EqualTo(1));
    }

    [Test]
    public async Task IntegracionEconomica_EstadoAmbiguoExigeConciliacionYPermiteReintento()
    {
        var (_, oc) = await CrearOcLegacy();
        await using var cn = await Conexion();
        await cn.ExecuteAsync("UPDATE compras.OrdenCompra SET Estado='Aceptada' WHERE IdOrdenCompra=@oc", new { oc });
        Bloqueado(NormalizarHasta(), 51340);
        await cn.ExecuteAsync("""
            UPDATE compras.OrdenCompra
            SET IdMoneda=(SELECT IdMoneda FROM maestra.Moneda WHERE Codigo='PEN')
            WHERE IdOrdenCompra=@oc;
            """, new { oc });
        Assert.That(NormalizarHasta().Successful, Is.True);

        Bloqueado(IntegrarEconomia(), 51401);
        Assert.That(await cn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ScriptName LIKE '%V2_2_0__%'"), Is.Zero);

        await cn.ExecuteAsync("UPDATE compras.OrdenCompra SET Estado='APROBADA' WHERE IdOrdenCompra=@oc", new { oc });
        var reintento = IntegrarEconomia();
        Assert.That(reintento.Successful, Is.True, reintento.Error?.ToString());
        Assert.That(await cn.QuerySingleAsync<string>(
            "SELECT Estado FROM compras.OrdenCompra WHERE IdOrdenCompra=@oc", new { oc }), Is.EqualTo("APROBADA"));
    }

    [Test]
    public async Task IntegracionEconomica_MultiplesComprasExigeConciliacionSinEliminarDatos()
    {
        var (_, oc) = await CrearOcLegacy();
        await using var cn = await Conexion();
        Bloqueado(NormalizarHasta(), 51340);
        await cn.ExecuteAsync("""
            UPDATE compras.OrdenCompra
            SET IdMoneda=(SELECT IdMoneda FROM maestra.Moneda WHERE Codigo='PEN')
            WHERE IdOrdenCompra=@oc;
            """, new { oc });
        Assert.That(NormalizarHasta().Successful, Is.True);
        await cn.ExecuteAsync("""
            INSERT INTO compras.Compra (NumeroCompra,IdOrdenCompra,FechaCompra,Aceptada)
            VALUES ('C1',@oc,GETDATE(),0),('C2',@oc,GETDATE(),0);
            """, new { oc });

        Bloqueado(IntegrarEconomia(), 51402);
        Assert.That(await cn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM compras.Compra WHERE IdOrdenCompra=@oc", new { oc }), Is.EqualTo(2));
        Assert.That(await cn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('compras.Compra') AND name='UX_Compra_IdOrdenCompra'"), Is.Zero);
    }

    private record Moneda(int IdMoneda,string Codigo,string Nombre,string Simbolo,bool Activo);
}
