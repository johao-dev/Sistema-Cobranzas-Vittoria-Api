using System.Data;
using System.Net.Http.Json;
using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Domain.Persistence;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

[NonParallelizable]
public class RequerimientoItemRepositoryTests : IntegrationTestBase
{
    private static IRequerimientoRepository Repository => GlobalSetupFixture.Factory.Services
        .GetRequiredService<IRequerimientoRepository>();

    [Test]
    public async Task ObtenerConItems_LeeFechasDecimalesYPartidasNulas()
    {
        var id = await CrearAsync();
        var requerimiento = await Repository.ObtenerConItemsAsync(id);
        Assert.That(requerimiento, Is.Not.Null);
        Assert.That(requerimiento!.NumeroRequerimiento, Is.Not.Empty);
        Assert.That(requerimiento.Descripcion, Is.Null);
        Assert.That(requerimiento.FechaEntrega, Is.Null);
        Assert.That(requerimiento.FechaRequerimiento, Is.EqualTo(DateOnly.FromDateTime(DateTime.Today)));
        Assert.That(requerimiento.Items.Single().Cantidad, Is.EqualTo(1.25m));
        Assert.That(requerimiento.Items.Single().IdPresupuestoDetalle, Is.Null);
        Assert.That(requerimiento.Items.Single().IdRequerimiento, Is.EqualTo(id));
        Assert.That(await Repository.ObtenerConItemsAsync(int.MaxValue), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Agregar_DevuelveIdentidadPersistidaYConservaElDocumento(bool conPartida)
    {
        var id = await CrearAsync();
        int? partida = conPartida ? await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync() : null;
        var item = await Repository.AgregarItemAsync(id, ItemRequerimiento.Crear(id, 6, partida, 0.25m, "Nuevo"));
        var recargado = await Repository.ObtenerConItemsAsync(id);
        Assert.That(item.IdRequerimientoDetalle, Is.GreaterThan(0));
        Assert.That(item.Cantidad, Is.EqualTo(0.25m));
        Assert.That(item.IdPresupuestoDetalle, Is.EqualTo(partida));
        Assert.That(recargado!.Items, Has.Count.EqualTo(2));
        Assert.That(recargado.Estado, Is.EqualTo(EstadoRequerimiento.EnviadoAlmacen));
        Assert.That(recargado.Observacion, Is.EqualTo("Cabecera"));
    }

    [Test]
    public async Task Actualizar_SoloCamposSolicitados_NoSobrescribeUnaLecturaAnterior()
    {
        var id = await CrearAsync();
        var anterior = (await Repository.ObtenerConItemsAsync(id))!;
        var item = anterior.Items.Single();
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        await cn.ExecuteAsync("UPDATE compras.RequerimientoDetalle SET Observacion=N'Cambio concurrente' WHERE IdRequerimiento=@id",
            new { id });
        var actualizado = anterior.ActualizarItem(item.IdRequerimientoDetalle, 3.75m, null, item.Observacion);
        var resultado = await Repository.ActualizarItemAsync(id, actualizado, CamposItemRequerimiento.Cantidad);
        Assert.That(resultado.Cantidad, Is.EqualTo(3.75m));
        Assert.That(resultado.Observacion, Is.EqualTo("Cambio concurrente"));
    }

    [Test]
    public async Task Actualizar_PartidaYObservacionNulas_LimpiaAmbasColumnas()
    {
        var id = await CrearAsync();
        var partida = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var item = await Repository.AgregarItemAsync(id, ItemRequerimiento.Crear(id, 6, partida, 2m, "Línea"));
        var actualizado = ItemRequerimiento.Reconstruir(item.IdRequerimientoDetalle, id, 6, null, 2m, null);
        var resultado = await Repository.ActualizarItemAsync(id, actualizado,
            CamposItemRequerimiento.Partida | CamposItemRequerimiento.Observacion);
        Assert.That(resultado.IdPresupuestoDetalle, Is.Null);
        Assert.That(resultado.Observacion, Is.Null);
    }

    [Test]
    public async Task Escribir_EstadoCambioDesdeLaLectura_RechazaTodasLasOperaciones()
    {
        var id = await CrearAsync();
        var req = (await Repository.ObtenerConItemsAsync(id))!;
        var existente = req.Items.Single();
        var nuevo = req.AgregarItem(6, 1m, null, null);
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        await cn.ExecuteAsync("UPDATE compras.Requerimiento SET Estado=N'ValidadoAlmacen' WHERE IdRequerimiento=@id", new { id });
        Assert.ThrowsAsync<ConflictoComprasException>(() => Repository.AgregarItemAsync(id, nuevo));
        Assert.ThrowsAsync<ConflictoComprasException>(() => Repository.ActualizarItemAsync(id, existente, CamposItemRequerimiento.Todos));
        Assert.ThrowsAsync<ConflictoComprasException>(() => Repository.EliminarItemAsync(id, existente.IdRequerimientoDetalle));
        Assert.That((await Repository.ObtenerConItemsAsync(id))!.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Escribir_ConDetalleAjeno_NoTocaNingunRequerimiento()
    {
        var id = await CrearAsync();
        var otro = await CrearAsync();
        var ajeno = (await Repository.ObtenerConItemsAsync(otro))!.Items.Single();
        var falseado = ItemRequerimiento.Reconstruir(ajeno.IdRequerimientoDetalle, id, 2, null, 99m, null);
        Assert.ThrowsAsync<RecursoComprasNoEncontradoException>(() =>
            Repository.ActualizarItemAsync(id, falseado, CamposItemRequerimiento.Cantidad));
        Assert.ThrowsAsync<RecursoComprasNoEncontradoException>(() => Repository.EliminarItemAsync(id, ajeno.IdRequerimientoDetalle));
        Assert.That((await Repository.ObtenerConItemsAsync(otro))!.Items.Single().Cantidad, Is.EqualTo(1.25m));
    }

    [Test]
    public async Task Agregar_ConMaterialOPartidaInexistentes_NoInserta()
    {
        var id = await CrearAsync();
        Assert.ThrowsAsync<ConflictoComprasException>(() =>
            Repository.AgregarItemAsync(id, ItemRequerimiento.Crear(id, int.MaxValue, null, 1m, null)));
        Assert.ThrowsAsync<ConflictoComprasException>(() =>
            Repository.AgregarItemAsync(id, ItemRequerimiento.Crear(id, 6, int.MaxValue, 1m, null)));
        Assert.That((await Repository.ObtenerConItemsAsync(id))!.Items, Has.Count.EqualTo(1));
    }

    [TestCase("otro-proyecto")]
    [TestCase("historico")]
    [TestCase("inactivo")]
    public async Task Agregar_ConPartidaIncompatible_Rechaza(string caso)
    {
        var id = await CrearAsync();
        var partida = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        var sql = caso switch
        {
            "otro-proyecto" => """
                UPDATE cc SET IdProyecto=NULL FROM ControlPresupuestario.CentroCosto cc
                JOIN ControlPresupuestario.Presupuesto p ON p.IdCentroCosto=cc.IdCentroCosto
                JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuesto=p.IdPresupuesto
                JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion
                WHERE pd.IdPresupuestoDetalle=@partida
                """,
            "historico" => """
                UPDATE pv SET IdEstadoPresupuesto=(SELECT IdEstadoPresupuesto
                    FROM ControlPresupuestario.EstadoPresupuesto WHERE Codigo='HISTORICO')
                FROM ControlPresupuestario.PresupuestoVersion pv
                JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion
                WHERE pd.IdPresupuestoDetalle=@partida
                """,
            _ => """
                UPDATE p SET Activo=0 FROM ControlPresupuestario.Presupuesto p
                JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuesto=p.IdPresupuesto
                JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion
                WHERE pd.IdPresupuestoDetalle=@partida
                """
        };
        await cn.ExecuteAsync(sql, new { partida });
        var error = Assert.ThrowsAsync<ConflictoComprasException>(() =>
            Repository.AgregarItemAsync(id, ItemRequerimiento.Crear(id, 6, partida, 1m, null)));
        Assert.That(error!.CodigoError, Is.EqualTo("PARTIDA_REQUERIMIENTO_INVALIDA"));
    }

    [Test]
    public async Task AgregarConcurrente_MismoMaterial_SoloUnaOperacionSeConfirma()
    {
        var id = await CrearAsync();
        async Task<bool> Agregar()
        {
            try { await Repository.AgregarItemAsync(id, ItemRequerimiento.Crear(id, 6, null, 1m, null)); return true; }
            catch (ConflictoComprasException) { return false; }
        }
        var resultados = await Task.WhenAll(Agregar(), Agregar());
        Assert.That(resultados.Count(x => x), Is.EqualTo(1));
        Assert.That((await Repository.ObtenerConItemsAsync(id))!.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task EliminarConcurrente_DosLineas_NuncaDejaElRequerimientoVacio()
    {
        var id = await CrearAsync();
        var agregado = await Repository.AgregarItemAsync(id, ItemRequerimiento.Crear(id, 6, null, 1m, null));
        var req = (await Repository.ObtenerConItemsAsync(id))!;
        async Task<bool> Eliminar(int detalle)
        {
            try { await Repository.EliminarItemAsync(id, detalle); return true; }
            catch (ConflictoComprasException) { return false; }
        }
        var resultados = await Task.WhenAll(Eliminar(req.Items.First().IdRequerimientoDetalle),
            Eliminar(agregado.IdRequerimientoDetalle));
        Assert.That(resultados.Count(x => x), Is.EqualTo(1));
        Assert.That((await Repository.ObtenerConItemsAsync(id))!.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Agregar_MientrasOtraTransaccionCambiaElEstado_EsperaYRevalida()
    {
        var id = await CrearAsync();
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync("UPDATE compras.Requerimiento SET Estado=N'ValidadoAlmacen' WHERE IdRequerimiento=@id",
            new { id }, tx);
        var escritura = Repository.AgregarItemAsync(id, ItemRequerimiento.Crear(id, 6, null, 1m, null));
        try
        {
            Assert.That(await Task.WhenAny(escritura, Task.Delay(150)), Is.Not.SameAs(escritura));
        }
        finally { tx.Commit(); }
        Assert.ThrowsAsync<ConflictoComprasException>(async () => await escritura);
    }

    [Test]
    public async Task Agregar_ConTransaccionExterna_NoConfirmaLaTransaccionDelLlamador()
    {
        var id = await CrearAsync();
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        using var tx = cn.BeginTransaction();
        await cn.QuerySingleAsync<int>("compras.usp_RequerimientoItem_AgregarAlmacen",
            new { IdRequerimiento = id, IdMaterial = 6, Cantidad = 1m }, tx, commandType: CommandType.StoredProcedure);
        Assert.That(await cn.QuerySingleAsync<int>("SELECT @@TRANCOUNT", transaction: tx), Is.EqualTo(1));
        tx.Rollback();
        Assert.That((await Repository.ObtenerConItemsAsync(id))!.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SpDirecto_CantidadConMasDecimales_RechazaSinRedondear()
    {
        var id = await CrearAsync();
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        var error = Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => cn.ExecuteAsync(
            "compras.usp_RequerimientoItem_AgregarAlmacen",
            new { IdRequerimiento = id, IdMaterial = 6, Cantidad = 0.001m }, commandType: CommandType.StoredProcedure));
        Assert.That(error!.Number, Is.EqualTo(51460));
    }

    [Test]
    public async Task ObtenerConItems_ConFechaEntrega_ConvierteDateNullableSinPerderElDia()
    {
        var id = await CrearAsync();
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        var entrega = DateTime.Today.AddDays(3);
        await cn.ExecuteAsync("UPDATE compras.Requerimiento SET FechaEntrega=@entrega WHERE IdRequerimiento=@id",
            new { id, entrega });
        Assert.That((await Repository.ObtenerConItemsAsync(id))!.FechaEntrega,
            Is.EqualTo(DateOnly.FromDateTime(entrega)));
    }

    [Test]
    public async Task ProcesarStockConcurrente_SoloRegistraUnaValidacion()
    {
        var id = await CrearAsync();
        async Task<bool> Procesar()
        {
            await using var cn = await DbHelpers.OpenTestConnectionAsync();
            try
            {
                await cn.ExecuteAsync("compras.usp_Requerimiento_ProcesarStock",
                    new { IdRequerimiento = id, IdUsuario = SeedIds.AlmacenId, Resultado = "Conforme" },
                    commandType: CommandType.StoredProcedure);
                return true;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 51055) { return false; }
        }
        var resultados = await Task.WhenAll(Procesar(), Procesar());
        Assert.That(resultados.Count(x => x), Is.EqualTo(1));
        Assert.That(await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM compras.RequerimientoValidacion WHERE IdRequerimiento=@id", new { id }), Is.EqualTo(1));
    }

    [Test]
    public async Task Restricciones_ProtegenCantidadYUnicidadInclusoConSqlDirecto()
    {
        var id = await CrearAsync();
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        var cantidad = Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => cn.ExecuteAsync(
            "UPDATE compras.RequerimientoDetalle SET Cantidad=0 WHERE IdRequerimiento=@id", new { id }));
        Assert.That(cantidad!.Number, Is.EqualTo(547));
        var duplicado = Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => cn.ExecuteAsync("""
            INSERT INTO compras.RequerimientoDetalle (IdRequerimiento, IdMaterial, Cantidad)
            VALUES (@id, 2, 1);
            """, new { id }));
        Assert.That(duplicado!.Number, Is.EqualTo(2627));
        Assert.That((await Repository.ObtenerConItemsAsync(id))!.Items.Single().Cantidad, Is.EqualTo(1.25m));
    }

    [Test]
    public async Task Actualizar_PartidaIncompatible_NoAplicaTampocoLosOtrosCambios()
    {
        var id = await CrearAsync();
        var anterior = (await Repository.ObtenerConItemsAsync(id))!.Items.Single();
        var item = ItemRequerimiento.Reconstruir(anterior.IdRequerimientoDetalle, id, 2, int.MaxValue, 99m, "Cambio");
        Assert.ThrowsAsync<ConflictoComprasException>(() =>
            Repository.ActualizarItemAsync(id, item, CamposItemRequerimiento.Todos));
        var actual = (await Repository.ObtenerConItemsAsync(id))!.Items.Single();
        Assert.That((actual.Cantidad, actual.Observacion), Is.EqualTo((1.25m, "Original")));
    }

    private async Task<int> CrearAsync()
    {
        var id = await RequerimientoBuilder.Nuevo().ConObservacion("Cabecera").ConItem(2, 1.25m, "Original").CrearAsync(_client);
        var enviar = await _client.PostAsJsonAsync($"/api/compras/requerimientos/{id}/enviar", new { observacion = (string?)null });
        Assert.That(enviar.IsSuccessStatusCode, Is.True, await enviar.Content.ReadAsStringAsync());
        return id;
    }
}
