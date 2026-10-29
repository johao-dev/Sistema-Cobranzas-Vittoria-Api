using System.Data;
using System.Net;
using System.Net.Http.Json;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Inventario;

[NonParallelizable]
public class StockProyectoTests : IntegrationTestBase
{
    private const int ORIGEN = 10;
    private const int DESTINO = 20;
    private const int MATERIAL = 2;
    private const int OTRO_MATERIAL = 11;

    private static SqlConnection Conexion() => new(GlobalSetupFixture.DbContainer.GetConnectionString());

    [SetUp]
    public async Task PrepararProyectoDestino()
    {
        await using SqlConnection db = Conexion();
        await db.ExecuteAsync("""
            IF NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = 20)
            BEGIN
                SET IDENTITY_INSERT maestra.Proyecto ON;
                INSERT INTO maestra.Proyecto (IdProyecto, NombreProyecto, Descripcion, Activo, FechaCreacion, CotizacionGeneral)
                VALUES (20, N'Proyecto Test Secundario', N'Solo para tests', 1, GETDATE(), 0);
                SET IDENTITY_INSERT maestra.Proyecto OFF;
            END;
            """);
    }

    private static async Task<int> Entrada(decimal cantidad, int proyecto = ORIGEN, int material = MATERIAL, int? id = null)
    {
        await using SqlConnection db = Conexion();
        DynamicParameters parametros = new(new
        {
            IdMaterial = material, IdProyecto = proyecto, Cantidad = cantidad, Fecha = new DateTime(2026, 10, 22)
        });
        if (id.HasValue) parametros.Add("IdKardexEntrada", id.Value);
        KardexEntrada fila = await db.QuerySingleAsync<KardexEntrada>(
            id.HasValue ? "almacen.usp_KardexEntrada_Actualizar" : "almacen.usp_KardexEntrada_Registrar",
            parametros, commandType: CommandType.StoredProcedure);
        return fila.IdKardexEntrada;
    }

    private static async Task<int> Salida(int proyecto, int? id, params (int Material, decimal Cantidad)[] cantidades)
    {
        DataTable items = new();
        items.Columns.Add("IdMaterial", typeof(int));
        items.Columns.Add("Cantidad", typeof(decimal));
        items.Columns.Add("Observacion", typeof(string));
        foreach ((int material, decimal cantidad) in cantidades) items.Rows.Add(material, cantidad, DBNull.Value);
        DynamicParameters parametros = new(new
        {
            IdProyecto = proyecto, Fecha = new DateTime(2026, 10, 22), Solicitante = "Prueba stock por proyecto",
            Items = items.AsTableValuedParameter("almacen.TVP_KardexSalidaItem")
        });
        if (id.HasValue) parametros.Add("IdKardexSalida", id.Value);
        await using SqlConnection db = Conexion();
        IEnumerable<KardexSalida> filas = await db.QueryAsync<KardexSalida>(
            id.HasValue ? "almacen.usp_KardexSalida_Actualizar" : "almacen.usp_KardexSalida_Registrar",
            parametros, commandType: CommandType.StoredProcedure);
        return filas.First().IdKardexSalida;
    }

    private async Task<HttpResponseMessage> Transferir(decimal cantidad, int origen = ORIGEN, int destino = DESTINO,
        params (int Material, decimal Cantidad)[] adicionales)
        => await _client.PostAsJsonAsync("/api/almacen/transferencias-proyecto", new
        {
            IdProyectoOrigen = origen, IdProyectoDestino = destino, Fecha = "2026-10-22", Observacion = "Prueba",
            Items = new[] { (Material: MATERIAL, Cantidad: cantidad) }.Concat(adicionales)
                .Select(item => new { IdMaterial = item.Material, item.Cantidad }).ToArray()
        });

    private static async Task Eliminar(string tipo, int id)
    {
        await using SqlConnection db = Conexion();
        DynamicParameters parametros = new();
        parametros.Add(tipo == "Entrada" ? "IdKardexEntrada" : "IdKardexSalida", id);
        await db.ExecuteAsync($"almacen.usp_Kardex{tipo}_Eliminar", parametros, commandType: CommandType.StoredProcedure);
    }

    private static async Task<decimal> Stock(int proyecto, int material = MATERIAL)
    {
        await using SqlConnection db = Conexion();
        return await db.QuerySingleAsync<decimal>("""
            SELECT ISNULL((SELECT Stock FROM almacen.StockProyecto WHERE IdProyecto = @proyecto AND IdMaterial = @material), 0)
            """, new { proyecto, material });
    }

    private static async Task<string> Global()
    {
        await using SqlConnection db = Conexion();
        return await db.QuerySingleAsync<string>("""
            SELECT (SELECT * FROM almacen.KardexStock ORDER BY IdMaterial FOR JSON PATH)
            """);
    }

    private static async Task Consistente()
    {
        await using SqlConnection db = Conexion();
        int diferencias = await db.QuerySingleAsync<int>("""
            SELECT COUNT(*) FROM almacen.KardexStock k
            FULL JOIN (SELECT IdMaterial, SUM(Stock) AS Stock FROM almacen.StockProyecto GROUP BY IdMaterial) s
                ON s.IdMaterial = k.IdMaterial
            WHERE ISNULL(k.Stock, 0) <> ISNULL(s.Stock, 0);
            """);
        Assert.That(diferencias, Is.Zero);
        Assert.That(await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.StockProyecto WHERE Stock < 0"), Is.Zero);
    }

    [Test]
    public async Task Entradas_PrimeraAdicionalYDistintosProyectos_ConservanCentavos()
    {
        await Entrada(6.25m);
        await Entrada(1.15m);
        await Entrada(4.10m, DESTINO);
        Assert.That(await Stock(ORIGEN), Is.EqualTo(7.40m));
        Assert.That(await Stock(DESTINO), Is.EqualTo(4.10m));
        await Consistente();
    }

    [Test]
    public async Task Entrada_ActualizarCantidadMaterialYProyecto_AplicaDiferencias()
    {
        int id = await Entrada(10);
        int idSalida = await Salida(ORIGEN, null, (MATERIAL, 8));
        await Entrada(9, id: id); // Solo revierte la diferencia, aunque ya se consumieron 8.
        Assert.That(await Stock(ORIGEN), Is.EqualTo(1));
        await Entrada(12, id: id);
        Assert.That(await Stock(ORIGEN), Is.EqualTo(4));
        await Eliminar("Salida", idSalida);
        await Entrada(5.25m, DESTINO, OTRO_MATERIAL, id);
        Assert.That(await Stock(ORIGEN), Is.Zero);
        Assert.That(await Stock(DESTINO, OTRO_MATERIAL), Is.EqualTo(5.25m));
        await Eliminar("Entrada", id);
        Assert.That(await Stock(DESTINO, OTRO_MATERIAL), Is.Zero);
        await Consistente();
    }

    [Test]
    public async Task Entrada_CambiarSoloProyecto_ReasignaSinCambiarSaldoGlobal()
    {
        int id = await Entrada(10);
        await Entrada(10, DESTINO, id: id);
        Assert.That(await Stock(ORIGEN), Is.Zero);
        Assert.That(await Stock(DESTINO), Is.EqualTo(10));
        await Consistente();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Entrada_ConsumidaPorTransferencia_RechazaReversionSinCambiarNada(bool eliminar)
    {
        int id = await Entrada(10);
        Assert.That((await Transferir(6)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        string global = await Global();
        SqlException? error = Assert.ThrowsAsync<SqlException>(async () =>
        {
            if (eliminar) await Eliminar("Entrada", id);
            else await Entrada(3, id: id);
        });
        Assert.That(error!.Number, Is.EqualTo(51114));
        Assert.That(error.Message, Does.Contain("idProyecto=10").And.Contain("disponible=4"));
        Assert.That(await Global(), Is.EqualTo(global));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(4));
        await Consistente();
    }

    [TestCase(5, false)]
    [TestCase(4, true)]
    public async Task Salida_DisponibilidadGlobalNoSustituyeLaDelProyecto(decimal cantidad, bool valida)
    {
        await Entrada(6);
        await Entrada(4, DESTINO);
        if (valida)
        {
            await Salida(DESTINO, null, (MATERIAL, cantidad));
            Assert.That(await Stock(DESTINO), Is.Zero);
        }
        else
        {
            SqlException? error = Assert.ThrowsAsync<SqlException>(() => Salida(DESTINO, null, (MATERIAL, cantidad)));
            Assert.That(error!.Number, Is.EqualTo(51114));
            Assert.That(error.Message, Does.Contain("idMaterial=2").And.Contain("idProyecto=20")
                .And.Contain("disponible=4").And.Contain("solicitado=5"));
            Assert.That(await Stock(DESTINO), Is.EqualTo(4));
        }
        await Consistente();
    }

    [TestCase(2, 2, true)]
    [TestCase(3, 2, false)]
    public async Task Salida_ConsolidaMaterialesRepetidosAntesDeValidar(decimal primera, decimal segunda, bool valida)
    {
        await Entrada(10);
        await Entrada(4, DESTINO);
        if (valida) await Salida(DESTINO, null, (MATERIAL, primera), (MATERIAL, segunda));
        else Assert.That(Assert.ThrowsAsync<SqlException>(() => Salida(DESTINO, null, (MATERIAL, primera), (MATERIAL, segunda)))!.Number,
            Is.EqualTo(51114));
        Assert.That(await Stock(DESTINO), Is.EqualTo(valida ? 0 : 4));
        await Consistente();
    }

    [Test]
    public async Task Salida_ActualizarIncrementarReducirMaterialProyectoYEliminar()
    {
        await Entrada(10);
        await Entrada(6, DESTINO, OTRO_MATERIAL);
        int id = await Salida(ORIGEN, null, (MATERIAL, 8));
        await Salida(ORIGEN, id, (MATERIAL, 9));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(1));
        await Salida(ORIGEN, id, (MATERIAL, 3));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(7));
        await Salida(DESTINO, id, (OTRO_MATERIAL, 6));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(10));
        Assert.That(await Stock(DESTINO, OTRO_MATERIAL), Is.Zero);
        await Eliminar("Salida", id);
        Assert.That(await Stock(DESTINO, OTRO_MATERIAL), Is.EqualTo(6));
        await Consistente();
    }

    [Test]
    public async Task Salida_CambioSoloProyectoSinSaldo_RechazaYConservaSalidaAnterior()
    {
        await Entrada(10);
        int id = await Salida(ORIGEN, null, (MATERIAL, 3));
        string global = await Global();
        Assert.That(Assert.ThrowsAsync<SqlException>(() => Salida(DESTINO, id, (MATERIAL, 3)))!.Number, Is.EqualTo(51114));
        Assert.That(await Global(), Is.EqualTo(global));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(7));
        await using SqlConnection db = Conexion();
        Assert.That(await db.QuerySingleAsync<int>("SELECT IdProyecto FROM almacen.KardexSalida WHERE IdKardexSalida = @id", new { id }), Is.EqualTo(ORIGEN));
        await Consistente();
    }

    [TestCase(4)]
    [TestCase(10)]
    public async Task Transferencia_Valida_CreaDestinoYConservaGlobalCompleto(decimal cantidad)
    {
        await Entrada(10);
        string global = await Global();
        HttpResponseMessage response = await Transferir(cantidad);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        List<TransferenciaProyecto> filas = (await response.Content.ReadFromJsonAsync<List<TransferenciaProyecto>>())!;
        Assert.That(filas, Has.Count.EqualTo(1));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(10 - cantidad));
        Assert.That(await Stock(DESTINO), Is.EqualTo(cantidad));
        Assert.That(await Global(), Is.EqualTo(global));
        HttpResponseMessage consulta = await _client.GetAsync($"/api/almacen/transferencias-proyecto/{filas[0].IdTransferencia}");
        Assert.That(consulta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await Consistente();
    }

    [Test]
    public async Task Transferencia_DestinoExistenteYRepetidos_ConsolidaDetalle()
    {
        await Entrada(10);
        await Entrada(2, DESTINO);
        HttpResponseMessage response = await Transferir(2, ORIGEN, DESTINO, (MATERIAL, 3));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        List<TransferenciaProyecto> filas = (await response.Content.ReadFromJsonAsync<List<TransferenciaProyecto>>())!;
        Assert.That(filas.Single().Cantidad, Is.EqualTo(5));
        Assert.That(await Stock(DESTINO), Is.EqualTo(7));
        await Consistente();
    }

    [TestCase(10, 10, 1, "PROYECTOS_IGUALES")]
    [TestCase(10, 20, 11, "STOCK_PROYECTO_INSUFICIENTE")]
    [TestCase(10, 999999, 1, "FK_NO_EXISTE")]
    public async Task Transferencia_Invalida_NoPersisteNiCambiaSaldos(int origen, int destino, decimal cantidad, string codigo)
    {
        await Entrada(10);
        string global = await Global();
        HttpResponseMessage response = await Transferir(cantidad, origen, destino);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity), await response.Content.ReadAsStringAsync());
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain(codigo));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(10));
        Assert.That(await Global(), Is.EqualTo(global));
        await using SqlConnection db = Conexion();
        Assert.That(await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.TransferenciaProyecto"), Is.Zero);
        await Consistente();
    }

    [TestCase("Entrada")]
    [TestCase("Salida")]
    [TestCase("TransferenciaProyectoDetalle")]
    public async Task ErrorPersistencia_RevierteCabeceraDetallesYAmbosStocks(string tipo)
    {
        await Entrada(10);
        string global = await Global();
        string tabla = tipo == "Entrada" ? "StockProyecto" : tipo == "Salida" ? "KardexSalida" : tipo;
        string evento = tipo == "Entrada" ? "UPDATE" : "INSERT";
        await using SqlConnection db = Conexion();
        await db.ExecuteAsync($"""
            CREATE OR ALTER TRIGGER almacen.TR_StockProyecto_Prueba ON almacen.{tabla} AFTER {evento} AS
            BEGIN THROW 51198, 'ERROR_PRUEBA: fallo deliberado de persistencia.', 1; END;
            """);
        try
        {
            if (tipo == "TransferenciaProyectoDetalle")
            {
                HttpResponseMessage response = await Transferir(3);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            }
            else
            {
                SqlException? error = Assert.ThrowsAsync<SqlException>(async () =>
                {
                    if (tipo == "Entrada") await Entrada(3);
                    else await Salida(ORIGEN, null, (MATERIAL, 3));
                });
                Assert.That(error!.Number, Is.EqualTo(51198));
            }
            Assert.That(await Global(), Is.EqualTo(global));
            Assert.That(await Stock(ORIGEN), Is.EqualTo(10));
            Assert.That(await Stock(DESTINO), Is.Zero);
            Assert.That(await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.TransferenciaProyecto"), Is.Zero);
            Assert.That(await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.KardexEntrada"), Is.EqualTo(1));
            Assert.That(await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.KardexSalida"), Is.Zero);
            await Consistente();
        }
        finally { await db.ExecuteAsync("DROP TRIGGER almacen.TR_StockProyecto_Prueba"); }
    }

    [TestCase("entrada")]
    [TestCase("salida")]
    [TestCase("transferencia")]
    public async Task InconsistenciaPrevia_SeRechazaSinCorregirAutomaticamente(string operacion)
    {
        await Entrada(10);
        await using SqlConnection db = Conexion();
        await db.ExecuteAsync("UPDATE almacen.StockProyecto SET Stock = 9 WHERE IdMaterial = 2");
        string global = await Global();
        if (operacion == "transferencia")
        {
            HttpResponseMessage response = await Transferir(1);
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("STOCK_PROYECTO_INCONSISTENTE"));
        }
        else
        {
            SqlException? error = Assert.ThrowsAsync<SqlException>(async () =>
            {
                if (operacion == "entrada") await Entrada(1);
                else await Salida(ORIGEN, null, (MATERIAL, 1));
            });
            Assert.That(error!.Number, Is.EqualTo(51116));
        }
        Assert.That(await Stock(ORIGEN), Is.EqualTo(9));
        Assert.That(await Global(), Is.EqualTo(global));
    }

    [Test]
    public async Task TransferenciaYSalidaConcurrentes_NoConsumenDosVecesSaldoOrigen()
    {
        await Entrada(10);
        HttpResponseMessage[] respuestas = await Task.WhenAll(Transferir(7), _client.PostAsJsonAsync("/api/almacen/kardex/salidas", new
        {
            IdProyecto = ORIGEN, Fecha = "2026-10-22", Solicitante = "Prueba",
            Items = new[] { new { IdMaterial = MATERIAL, Cantidad = 7 } }
        }));
        Assert.That(respuestas.Select(r => r.StatusCode), Is.EquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity }));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(3));
        await Consistente();
    }

    [Test]
    public async Task StockProyecto_ListadoFiltraProyectoMaterialEspecialidadYFechaCompleta()
    {
        await Entrada(10);
        await Transferir(3);
        await using SqlConnection db = Conexion();
        string hoy = await db.QuerySingleAsync<string>("SELECT CONVERT(VARCHAR(10), SYSDATETIME(), 23)");
        List<StockProyecto> filas = (await _client.GetFromJsonAsync<List<StockProyecto>>(
            $"/api/almacen/kardex/stock-proyecto?idProyecto=20&idMaterial=2&idEspecialidad=2&fechaDesde={hoy}&fechaHasta={hoy}"))!;
        Assert.That(filas.Single().Stock, Is.EqualTo(3));
        Assert.That(filas.Single().Especialidad, Is.Not.Empty);
        List<StockProyecto> vacio = (await _client.GetFromJsonAsync<List<StockProyecto>>(
            "/api/almacen/kardex/stock-proyecto?idProyecto=20&idEspecialidad=4"))!;
        Assert.That(vacio, Is.Empty);
    }

    [Test]
    public async Task Salida_CambiarSoloProyectoConSaldo_RevertirOrigenYConsumirDestino()
    {
        await Entrada(10);
        await Entrada(4, DESTINO);
        int id = await Salida(ORIGEN, null, (MATERIAL, 3));
        await Salida(DESTINO, id, (MATERIAL, 3));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(10));
        Assert.That(await Stock(DESTINO), Is.EqualTo(1));
        await Consistente();
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task TransferenciaSql_CantidadNoPositiva_DevuelveErrorDeNegocio(decimal cantidad)
    {
        await Entrada(10);
        await using SqlConnection db = Conexion();
        SqlException? error = Assert.ThrowsAsync<SqlException>(() => db.ExecuteAsync("""
            DECLARE @Items almacen.TVP_TransferenciaProyectoItem;
            INSERT INTO @Items VALUES (2, @cantidad);
            EXEC almacen.usp_TransferenciaProyecto_Registrar 10, 20, '20261022', N'Prueba', @Items;
            """, new { cantidad }));
        Assert.That(error!.Number, Is.EqualTo(51103));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(10));
        await Consistente();
    }

    [Test]
    public async Task Transferencia_MaterialInexistente_NoPersiste()
    {
        await Entrada(10);
        HttpResponseMessage response = await Transferir(1, ORIGEN, DESTINO, (999999, 1));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("FK_NO_EXISTE"));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(10));
        await Consistente();
    }

    [Test]
    public async Task AuxiliarDeEscrituraSinTransaccionNiBloqueo_SeRechaza()
    {
        await Entrada(10);
        await using SqlConnection db = Conexion();
        SqlException? error = Assert.ThrowsAsync<SqlException>(() => db.ExecuteAsync("""
            DECLARE @Cambios almacen.TVP_StockProyectoCantidad;
            INSERT INTO @Cambios VALUES (2, 10, -1);
            EXEC almacen.usp_StockProyecto_AplicarCambios @Cambios;
            """));
        Assert.That(error!.Number, Is.EqualTo(51118));
        Assert.That(await Stock(ORIGEN), Is.EqualTo(10));
        await Consistente();
    }

    [Test]
    public async Task Transferencia_ItemNuloYDecimalesExcesivos_DevuelvenValidacion()
    {
        await Entrada(10);
        HttpResponseMessage nulo = await _client.PostAsJsonAsync("/api/almacen/transferencias-proyecto", new
        {
            IdProyectoOrigen = ORIGEN, IdProyectoDestino = DESTINO, Fecha = "2026-10-22", Observacion = "Prueba",
            Items = new object?[] { null }
        });
        Assert.That(nulo.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity), await nulo.Content.ReadAsStringAsync());
        HttpResponseMessage precision = await Transferir(1.001m);
        Assert.That(precision.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity), await precision.Content.ReadAsStringAsync());
        Assert.That(await Stock(ORIGEN), Is.EqualTo(10));
        await Consistente();
    }

    [Test]
    public async Task Transferencia_Inexistente_Devuelve404MedianteMiddleware()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/almacen/transferencias-proyecto/999999");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("RECURSO_NO_ENCONTRADO"));
    }
}
