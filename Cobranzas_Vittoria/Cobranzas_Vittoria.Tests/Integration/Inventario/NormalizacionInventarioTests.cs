using System.Data;
using System.Net;
using System.Net.Http.Json;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Inventario.Presentation.Dto;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Inventario;

public class NormalizacionInventarioTests : IntegrationTestBase
{
    private const int MATERIAL_ALBANILERIA = 2;
    private const int MATERIAL_CASCO = 11;
    private readonly List<int> _materialesCreados = new();

    private static SqlConnection Conexion()
        => new(GlobalSetupFixture.DbContainer.GetConnectionString());

    private static KardexEntradaCreateDto Entrada(int idMaterial, decimal cantidad)
        => new()
        {
            IdMaterial = idMaterial,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Fecha = new DateOnly(2026, 10, 21),
            Cantidad = cantidad
        };

    private static KardexSalidaCreateDto Salida(params (int Material, decimal Cantidad)[] items)
        => new()
        {
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Fecha = new DateOnly(2026, 10, 21),
            Solicitante = "Prueba de normalización",
            Items = items.Select(item => new KardexSalidaItemCreateDto
            {
                IdMaterial = item.Material,
                Cantidad = item.Cantidad
            }).ToList()
        };

    private async Task<KardexEntradaResponseDto> RegistrarEntradaAsync(int idMaterial, decimal cantidad)
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/almacen/kardex/entradas", Entrada(idMaterial, cantidad));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<KardexEntradaResponseDto>())!;
    }

    private static async Task<decimal> StockAsync(int idMaterial)
    {
        await using SqlConnection connection = Conexion();
        return await connection.QuerySingleAsync<decimal>(
            "SELECT Stock FROM almacen.KardexStock WHERE IdMaterial = @idMaterial", new { idMaterial });
    }

    [Test]
    public async Task Salida_VariasEspecialidades_AjustaFiltraYReponeStockPorMaterial()
    {
        await RegistrarEntradaAsync(MATERIAL_ALBANILERIA, 10.25m);
        await RegistrarEntradaAsync(MATERIAL_CASCO, 20.50m);
        KardexSalidaCreateDto salida = Salida((MATERIAL_ALBANILERIA, 1.15m), (MATERIAL_CASCO, 2.25m));

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas", salida);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        List<KardexSalidaResponseDto> filas = (await response.Content.ReadFromJsonAsync<List<KardexSalidaResponseDto>>())!;
        Assert.That(filas.Select(fila => fila.IdEspecialidad), Is.EquivalentTo(new[] { 2, 4 }));
        Assert.That(await StockAsync(MATERIAL_ALBANILERIA), Is.EqualTo(9.10m));
        Assert.That(await StockAsync(MATERIAL_CASCO), Is.EqualTo(18.25m));

        List<KardexSalidaResponseDto> filtradas = (await _client.GetFromJsonAsync<List<KardexSalidaResponseDto>>(
            "/api/almacen/kardex/salidas?idEspecialidad=4"))!;
        Assert.That(filtradas, Has.Count.EqualTo(1));
        Assert.That(filtradas[0].IdMaterial, Is.EqualTo(MATERIAL_CASCO));

        salida.IdKardexSalida = filas[0].IdKardexSalida;
        salida.Items[0].Cantidad = 0.05m;
        salida.Items[1].Cantidad = 7.25m;
        response = await _client.PutAsJsonAsync($"/api/almacen/kardex/salidas/{salida.IdKardexSalida}", salida);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(await StockAsync(MATERIAL_ALBANILERIA), Is.EqualTo(10.20m));
        Assert.That(await StockAsync(MATERIAL_CASCO), Is.EqualTo(13.25m));

        response = await _client.DeleteAsync($"/api/almacen/kardex/salidas/{salida.IdKardexSalida}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await StockAsync(MATERIAL_ALBANILERIA), Is.EqualTo(10.25m));
        Assert.That(await StockAsync(MATERIAL_CASCO), Is.EqualTo(20.50m));
    }

    [Test]
    public async Task Salida_MaterialRepetido_ValidaLaSumaYRevierteElRechazo()
    {
        await RegistrarEntradaAsync(MATERIAL_ALBANILERIA, 5m);
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas",
            Salida((MATERIAL_ALBANILERIA, 3m), (MATERIAL_ALBANILERIA, 3m)));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("STOCK_INSUFICIENTE"));
        Assert.That(await StockAsync(MATERIAL_ALBANILERIA), Is.EqualTo(5m));
        await using SqlConnection connection = Conexion();
        Assert.That(await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM almacen.KardexSalida"), Is.Zero);

        response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas",
            Salida((MATERIAL_ALBANILERIA, 1.20m), (MATERIAL_ALBANILERIA, 1.30m)));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await StockAsync(MATERIAL_ALBANILERIA), Is.EqualTo(2.50m));
    }

    [Test]
    public async Task Entrada_CambioDeMaterial_TransfiereStockYDerivaLaEspecialidad()
    {
        KardexEntradaResponseDto creada = await RegistrarEntradaAsync(MATERIAL_ALBANILERIA, 4.75m);
        KardexEntradaCreateDto modificada = Entrada(MATERIAL_CASCO, 3.25m);
        modificada.IdKardexEntrada = creada.IdKardexEntrada;

        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/almacen/kardex/entradas/{creada.IdKardexEntrada}", modificada);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        KardexEntradaResponseDto resultado = (await response.Content.ReadFromJsonAsync<KardexEntradaResponseDto>())!;
        Assert.That(resultado.IdEspecialidad, Is.EqualTo(SeedIds.EspecialidadCasco));
        Assert.That(await StockAsync(MATERIAL_ALBANILERIA), Is.Zero);
        Assert.That(await StockAsync(MATERIAL_CASCO), Is.EqualTo(3.25m));
    }

    [Test]
    public async Task EntradasConcurrentes_CreanUnaSolaFilaDeStockSinPerderCantidades()
    {
        HttpResponseMessage[] respuestas = await Task.WhenAll(
            _client.PostAsJsonAsync("/api/almacen/kardex/entradas", Entrada(MATERIAL_ALBANILERIA, 1.25m)),
            _client.PostAsJsonAsync("/api/almacen/kardex/entradas", Entrada(MATERIAL_ALBANILERIA, 1.25m)));

        Assert.That(respuestas.All(response => response.StatusCode == HttpStatusCode.OK), Is.True);
        Assert.That(await StockAsync(MATERIAL_ALBANILERIA), Is.EqualTo(2.50m));
        await using SqlConnection connection = Conexion();
        Assert.That(await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM almacen.KardexStock WHERE IdMaterial = @material", new { material = MATERIAL_ALBANILERIA }), Is.EqualTo(1));
    }

    [Test]
    public async Task SalidasConcurrentes_NoConsumenDosVecesElMismoSaldo()
    {
        await RegistrarEntradaAsync(MATERIAL_ALBANILERIA, 10m);
        HttpResponseMessage[] respuestas = await Task.WhenAll(
            _client.PostAsJsonAsync("/api/almacen/kardex/salidas", Salida((MATERIAL_ALBANILERIA, 7m))),
            _client.PostAsJsonAsync("/api/almacen/kardex/salidas", Salida((MATERIAL_ALBANILERIA, 7m))));

        Assert.That(respuestas.Select(response => response.StatusCode),
            Is.EquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity }));
        Assert.That(await StockAsync(MATERIAL_ALBANILERIA), Is.EqualTo(3m));
    }

    private async Task<int> CrearMaterialAsync()
    {
        await using SqlConnection connection = Conexion();
        int id = await connection.QuerySingleAsync<int>("""
            INSERT INTO maestra.Material (IdEspecialidad, Codigo, Descripcion, UnidadMedida)
            VALUES (2, @codigo, 'Prueba de clasificación', 'UND');
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { codigo = "NORM-" + Guid.NewGuid().ToString("N") });
        _materialesCreados.Add(id);
        return id;
    }

    [TestCase("entrada")]
    [TestCase("salida")]
    [TestCase("saldoCero")]
    [TestCase("legacy")]
    public async Task Material_ConMovimientos_BloqueaReclasificacionEnApiYSql(string origen)
    {
        int id = await CrearMaterialAsync();
        await using SqlConnection connection = Conexion();
        if (origen == "legacy")
        {
            await connection.ExecuteAsync("""
                INSERT INTO almacen.KardexMovimiento
                    (IdMaterial, IdEspecialidad, TipoMovimiento, FechaMovimiento, CantidadEntrada, CantidadSalida, StockResultante)
                VALUES (@id, 2, 'ENTRADA', GETDATE(), 1, 0, 1);
                """, new { id });
        }
        else if (origen == "salida")
        {
            HttpResponseMessage salida = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas", Salida((id, 0m)));
            Assert.That(salida.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            await connection.ExecuteAsync("DELETE FROM almacen.KardexStock WHERE IdMaterial = @id", new { id });
        }
        else
        {
            KardexEntradaResponseDto entrada = await RegistrarEntradaAsync(id, 1m);
            if (origen == "saldoCero")
                Assert.That((await _client.DeleteAsync($"/api/almacen/kardex/entradas/{entrada.IdKardexEntrada}")).StatusCode,
                    Is.EqualTo(HttpStatusCode.OK));
        }

        MaterialUpsertDto material = new() { IdEspecialidad = 4, Descripcion = "Edición rechazada", UnidadMedida = "UND" };
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/maestra/materiales/{id}", material);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), await response.Content.ReadAsStringAsync());
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("ESPECIALIDAD_MATERIAL_CON_MOVIMIENTOS"));

        SqlException? error = Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync(
            "UPDATE maestra.Material SET IdEspecialidad = 4 WHERE IdMaterial = @id", new { id }));
        Assert.That(error!.Number, Is.EqualTo(51112));
        Assert.That(await connection.QuerySingleAsync<int>("SELECT IdEspecialidad FROM maestra.Material WHERE IdMaterial = @id", new { id }), Is.EqualTo(2));

        material.IdEspecialidad = 2;
        material.Descripcion = "Edición permitida sin reclasificar";
        response = await _client.PutAsJsonAsync($"/api/maestra/materiales/{id}", material);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Entrada_RequestAntiguo_NoUsaLaEspecialidadEnviadaPorElCliente()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/almacen/kardex/entradas", new
        {
            IdMaterial = MATERIAL_ALBANILERIA,
            IdEspecialidad = 999999,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Fecha = "2026-10-21",
            Cantidad = 1m
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        KardexEntradaResponseDto entrada = (await response.Content.ReadFromJsonAsync<KardexEntradaResponseDto>())!;
        Assert.That(entrada.IdEspecialidad, Is.EqualTo(SeedIds.EspecialidadAlbanileria));
    }

    [Test]
    public async Task ReclasificacionConcurrente_EsperaAlMovimientoYSeRechazaAlConfirmarlo()
    {
        int id = await CrearMaterialAsync();
        await using SqlConnection movimiento = Conexion();
        await movimiento.OpenAsync();
        await using SqlTransaction transaction = (SqlTransaction)await movimiento.BeginTransactionAsync();
        await movimiento.ExecuteAsync("almacen.usp_KardexEntrada_Registrar", new
        {
            IdMaterial = id,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Fecha = new DateTime(2026, 10, 21),
            Cantidad = 1m
        }, transaction, commandType: CommandType.StoredProcedure);

        await using SqlConnection reclasificacion = Conexion();
        await reclasificacion.OpenAsync();
        await reclasificacion.ExecuteAsync("SET LOCK_TIMEOUT 100");
        SqlException? espera = Assert.ThrowsAsync<SqlException>(() => reclasificacion.ExecuteAsync(
            "UPDATE maestra.Material SET IdEspecialidad = 4 WHERE IdMaterial = @id", new { id }));
        Assert.That(espera!.Number, Is.EqualTo(1222), "La escritura debe esperar al movimiento aún no confirmado.");

        await transaction.CommitAsync();
        SqlException? rechazo = Assert.ThrowsAsync<SqlException>(() => reclasificacion.ExecuteAsync(
            "UPDATE maestra.Material SET IdEspecialidad = 4 WHERE IdMaterial = @id", new { id }));
        Assert.That(rechazo!.Number, Is.EqualTo(51112));
    }

    [Test]
    public async Task Material_SinMovimientos_PermiteCambiarEspecialidad()
    {
        int id = await CrearMaterialAsync();
        MaterialUpsertDto material = new() { IdEspecialidad = 4, Descripcion = "Material reclasificado", UnidadMedida = "UND" };
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/maestra/materiales/{id}", material);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        KardexEntradaResponseDto entrada = await RegistrarEntradaAsync(id, 1m);
        Assert.That(entrada.IdEspecialidad, Is.EqualTo(4));
    }

    [TearDown]
    public async Task LimpiarMaterialesCreados()
    {
        await using SqlConnection connection = Conexion();
        foreach (int id in _materialesCreados)
        {
            await connection.ExecuteAsync("""
                DELETE FROM almacen.KardexSalida WHERE IdKardexSalida IN
                    (SELECT IdKardexSalida FROM almacen.KardexSalidaDetalle WHERE IdMaterial = @id);
                DELETE FROM almacen.KardexEntrada WHERE IdMaterial = @id;
                DELETE FROM almacen.KardexStock WHERE IdMaterial = @id;
                DELETE FROM almacen.KardexMovimiento WHERE IdMaterial = @id;
                DELETE FROM maestra.Material WHERE IdMaterial = @id;
                """, new { id });
        }
        _materialesCreados.Clear();
    }
}
