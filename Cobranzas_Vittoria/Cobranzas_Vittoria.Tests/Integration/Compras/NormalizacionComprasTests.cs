using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

[NonParallelizable]
public class NormalizacionComprasTests : IntegrationTestBase
{
    private async Task<SqlConnection> Conexion()
    {
        var cn = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await cn.OpenAsync();
        return cn;
    }

    [Test]
    public async Task Schema_FuentesAutoritativasConstraintsYDecimales()
    {
        await using var cn = await Conexion();
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.columns WHERE
                (object_id=OBJECT_ID('compras.Requerimiento') AND name IN ('IdEspecialidad','IdPresupuestoDetalle')) OR
                (object_id=OBJECT_ID('compras.OrdenCompra') AND name IN ('IdProyecto','IdProveedor')) OR
                (object_id=OBJECT_ID('compras.Compra') AND name='IdProveedor');
            """), Is.Zero);
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.columns WHERE
                object_id IN (OBJECT_ID('compras.OrdenCompraDetalle'),OBJECT_ID('compras.CompraDetalle'))
                AND name='Subtotal' AND precision=18 AND scale=2 AND is_computed=1;
            """), Is.EqualTo(2));
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('compras.RequerimientoDetalle')
                AND name='IdPresupuestoDetalle' AND is_nullable=1;
            """), Is.EqualTo(1));
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('compras.OrdenCompra')
                AND name='IdMoneda' AND is_nullable=0;
            """), Is.EqualTo(1));
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM maestra.Moneda WHERE Codigo IN ('PEN','USD')"), Is.EqualTo(2));
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.objects WHERE object_id IN
                (OBJECT_ID('ControlPresupuestario.Moneda'),OBJECT_ID('compras.vw_Compra_ListadoConEspecialidad'));
            """), Is.Zero);
        Assert.That(await cn.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM sys.sql_modules WHERE definition LIKE '%ControlPresupuestario.Moneda%';
            """), Is.Zero);
    }

    private async Task<(int Oc, int Proveedor1, int Proveedor2)> CrearMultiproveedor()
    {
        await using var cn = await Conexion();
        var proveedores = (await cn.QueryAsync<int>("SELECT TOP (2) IdProveedor FROM maestra.Proveedor WHERE Activo=1 ORDER BY IdProveedor")).ToArray();
        Assert.That(proveedores.Length, Is.EqualTo(2));
        var req = await RequerimientoBuilder.Nuevo().ConItem(2, 5).ConItem(6, 3).CrearEnviadoOcAsync(_client);
        var dto = new OrdenCompraCreateDto
        {
            IdRequerimiento = req, IdMoneda = await DbHelpersMoneda.ObtenerPenAsync(),
            Items = [new() { IdMaterial=2,Cantidad=5,PrecioUnitario=20,IdProveedor=proveedores[0] },
                     new() { IdMaterial=6,Cantidad=3,PrecioUnitario=100,IdProveedor=proveedores[1] }]
        };
        var response = await _client.PostAsJsonAsync("/api/compras/ordenes-compra", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (result.GetProperty("idOrdenCompra").GetInt32(),proveedores[0],proveedores[1]);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task MultiplesEspecialidadesYProveedores_ProyectoMonedaYTotales(bool incluyeIgv)
    {
        var (oc,p1,p2) = await CrearMultiproveedor();
        var orden = await _client.GetFromJsonAsync<JsonElement>($"/api/compras/ordenes-compra/{oc}");
        var head = orden.GetProperty("ordenCompra");
        Assert.That(head.TryGetProperty("idProveedor",out _), Is.False);
        Assert.That(head.GetProperty("idProyecto").GetInt32(), Is.EqualTo(SeedIds.ProyectoMaytaCapacII));
        Assert.That(head.GetProperty("codigoMoneda").GetString(), Is.EqualTo("PEN"));
        Assert.That(head.GetProperty("especialidades").GetString(), Does.Contain(", "));
        Assert.That(head.GetProperty("proveedores").GetString(), Does.Contain(", "));
        var req = await _client.GetFromJsonAsync<JsonElement>($"/api/compras/requerimientos/{head.GetProperty("idRequerimiento").GetInt32()}");
        Assert.That(req.GetProperty("requerimiento").TryGetProperty("idEspecialidad",out _), Is.False);
        Assert.That(req.GetProperty("requerimiento").TryGetProperty("idPresupuestoDetalle",out _), Is.False);
        var response = await _client.PostAsJsonAsync("/api/compras/compras",new CompraCreateDto
        {
            IdOrdenCompra=oc, IncluyeIGV=incluyeIgv,
            Items=[new(){IdMaterial=2,Cantidad=5,PrecioUnitario=20},new(){IdMaterial=6,Cantidad=3,PrecioUnitario=100}]
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var compra = await _client.GetFromJsonAsync<JsonElement>($"/api/compras/compras/{created.GetProperty("idCompra").GetInt32()}");
        var cabecera = JsonHelpers.GetProp(compra,"compra");
        Assert.That(cabecera.TryGetProperty("IdProveedor",out _),Is.False);
        Assert.That(JsonHelpers.GetString(cabecera,"Proveedores"),Does.Contain(", "));
        Assert.That(JsonHelpers.GetDecimal(cabecera,"MontoTotal"),Is.EqualTo(400m));
        Assert.That(JsonHelpers.GetDecimal(cabecera,"MontoIGV"),Is.EqualTo(incluyeIgv ? 61.02m : 0m));
        var items=JsonHelpers.GetProp(compra,"items");
        Assert.That(items.EnumerateArray().Select(i=>JsonHelpers.GetInt32(i,"IdProveedor")),Is.EquivalentTo(new[]{p1,p2}));
    }

    [Test]
    public async Task Compra_ProveedorSeDerivaSoloDeLosMaterialesComprados()
    {
        var (oc,_,p2)=await CrearMultiproveedor();
        var created=await _client.PostAsJsonAsync("/api/compras/compras",new CompraCreateDto
        {
            IdOrdenCompra=oc,Items=[new(){IdMaterial=2,Cantidad=1,PrecioUnitario=20}]
        });
        Assert.That(created.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        var filtrado=await _client.GetFromJsonAsync<JsonElement>($"/api/compras/compras?idProveedor={p2}");
        Assert.That(filtrado.GetArrayLength(),Is.Zero);
    }

    [Test]
    public async Task Oc_ExigeMonedaYRechazaDuplicadosYPrecisionSinPersistir()
    {
        var req=await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var dto=new OrdenCompraCreateDto{IdRequerimiento=req,Items=[new(){IdMaterial=2,Cantidad=1,PrecioUnitario=1,IdProveedor=2}]};
        var sinMoneda=await _client.PostAsJsonAsync("/api/compras/ordenes-compra",dto);
        Assert.That(sinMoneda.StatusCode,Is.EqualTo(HttpStatusCode.BadRequest));
        dto.IdMoneda=await DbHelpersMoneda.ObtenerPenAsync();
        dto.Items.Add(dto.Items[0]);
        var repetidos=await _client.PostAsJsonAsync("/api/compras/ordenes-compra",dto);
        Assert.That(repetidos.StatusCode,Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        dto.Items.RemoveAt(1);dto.Items[0].Cantidad=1.001m;
        var precision=await _client.PostAsJsonAsync("/api/compras/ordenes-compra",dto);
        Assert.That(precision.StatusCode,Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM compras.OrdenCompra"),Is.Zero);
    }

    [Test]
    public async Task CompraYDb_RechazanMaterialesDuplicados()
    {
        var (oc,_,_)=await CrearMultiproveedor();
        var dto=new CompraCreateDto{IdOrdenCompra=oc,Items=[new(){IdMaterial=2,Cantidad=1,PrecioUnitario=20}]};
        dto.Items.Add(dto.Items[0]);
        var invalid=await _client.PostAsJsonAsync("/api/compras/compras",dto);
        Assert.That(invalid.StatusCode,Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM compras.Compra"),Is.Zero);
        dto.Items.RemoveAt(1);
        var response=await _client.PostAsJsonAsync("/api/compras/compras",dto);
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        var created=await response.Content.ReadFromJsonAsync<JsonElement>();
        await using var cn=await Conexion();
        var ex=Assert.ThrowsAsync<SqlException>(async()=>await cn.ExecuteAsync("""
            INSERT INTO compras.OrdenCompraDetalle (IdOrdenCompra,IdMaterial,IdProveedor,Cantidad,PrecioUnitario)
            VALUES (@oc,2,2,1,20);
            """,new{oc}));
        Assert.That(ex!.Number,Is.EqualTo(2627));
        ex=Assert.ThrowsAsync<SqlException>(async()=>await cn.ExecuteAsync("""
            INSERT INTO compras.CompraDetalle (IdCompra,IdMaterial,Cantidad,PrecioUnitario) VALUES (@id,2,1,20);
            """,new{id=created.GetProperty("idCompra").GetInt32()}));
        Assert.That(ex!.Number,Is.EqualTo(2627));
    }

    [Test]
    public async Task Requerimiento_PartidasDiferentesPorLinea_SeConservanAlEditar()
    {
        await using var cn=await Conexion();
        await cn.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo,Nombre) VALUES ('PROYECTO','Proyecto');
            INSERT INTO ControlPresupuestario.TipoPartida (Codigo,Nombre) VALUES ('MATERIALES','Materiales');
            INSERT INTO ControlPresupuestario.EstadoPresupuesto (Codigo,Nombre) VALUES ('BORRADOR','Borrador');
            INSERT INTO ControlPresupuestario.CentroCosto (Codigo,Nombre,IdTipoCentroCosto)
                SELECT 'TEST','Test',IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo='PROYECTO';
            """);
        var centro=await cn.QuerySingleAsync<int>("SELECT IdCentroCosto FROM ControlPresupuestario.CentroCosto WHERE Codigo='TEST'");
        var presupuesto=await cn.QuerySingleAsync<dynamic>("ControlPresupuestario.usp_Presupuesto_Crear",
            new{IdCentroCosto=centro,IdMoneda=await DbHelpersMoneda.ObtenerPenAsync(),Codigo="TEST",Nombre="Test"},commandType:CommandType.StoredProcedure);
        await cn.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.CatalogoPartida (Codigo,Nombre,IdTipoPartida,Nivel)
                SELECT v.Codigo,v.Codigo,t.IdTipoPartida,1 FROM (VALUES ('A'),('B')) v(Codigo)
                CROSS JOIN ControlPresupuestario.TipoPartida t WHERE t.Codigo='MATERIALES';
            INSERT INTO ControlPresupuestario.PresupuestoDetalle (IdPresupuestoVersion,IdCatalogoPartida,MontoPresupuestado)
                SELECT @version,IdCatalogoPartida,1000 FROM ControlPresupuestario.CatalogoPartida;
            """,new{version=(int)presupuesto.IdPresupuestoVersion});
        var partidas=(await cn.QueryAsync<int>("SELECT IdPresupuestoDetalle FROM ControlPresupuestario.PresupuestoDetalle ORDER BY IdPresupuestoDetalle")).ToArray();
        var req=await RequerimientoBuilder.Nuevo().ConItem(2,1,idPresupuestoDetalle:partidas[0]).ConItem(6,2,idPresupuestoDetalle:partidas[1]).CrearAsync(_client);
        var items=(await cn.QueryAsync<int>("SELECT IdPresupuestoDetalle FROM compras.RequerimientoDetalle WHERE IdRequerimiento=@req ORDER BY IdMaterial",new{req})).ToArray();
        Assert.That(items,Is.EqualTo(partidas));
        var updated=await _client.PutAsJsonAsync($"/api/compras/requerimientos/{req}",new RequerimientoUpdateDto
        {
            NumeroRequerimiento="EDITADO",FechaRequerimiento=DateTime.Today,IdProyecto=SeedIds.ProyectoMaytaCapacII,
            Items=[new(){IdMaterial=2,Cantidad=3,IdPresupuestoDetalle=partidas[1]},new(){IdMaterial=6,Cantidad=4,IdPresupuestoDetalle=partidas[0]}]
        });
        Assert.That(updated.StatusCode,Is.EqualTo(HttpStatusCode.OK),await updated.Content.ReadAsStringAsync());
        items=(await cn.QueryAsync<int>("SELECT IdPresupuestoDetalle FROM compras.RequerimientoDetalle WHERE IdRequerimiento=@req ORDER BY IdMaterial",new{req})).ToArray();
        Assert.That(items,Is.EqualTo(partidas.Reverse().ToArray()));
    }
}
