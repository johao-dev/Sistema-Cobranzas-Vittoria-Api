using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

[NonParallelizable]
public class RequerimientoItemControllerTests : IntegrationTestBase
{
    [Test]
    public async Task Agregar_Devuelve201ConIdentidadYConservaCabecera()
    {
        var id = await CrearAsync();
        var response = await _client.PostAsJsonAsync(Ruta(id), new { idMaterial = 6, cantidad = 0.25m, observacion = "Nuevo" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(item.GetProperty("idRequerimientoDetalle").GetInt32(), Is.GreaterThan(0));
        Assert.That(item.GetProperty("cantidad").GetDecimal(), Is.EqualTo(0.25m));
        Assert.That(item.GetProperty("idPresupuestoDetalle").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(await DbHelpers.QueryScalarAsync<string>("SELECT Observacion FROM compras.Requerimiento WHERE IdRequerimiento=@id",
            new { id }), Is.EqualTo("Cabecera"));
        Assert.That(await CantidadAsync(id), Is.EqualTo(2));
    }

    [Test]
    public async Task Actualizar_SoloCantidad_ConservaLaObservacionYLaIdentidadDelMaterial()
    {
        var id = await CrearAsync();
        var detalle = await IdDetalleAsync(id);
        var response = await _client.PatchAsJsonAsync(Ruta(id, detalle), new { cantidad = 2.75m });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(item.GetProperty("cantidad").GetDecimal(), Is.EqualTo(2.75m));
        Assert.That(item.GetProperty("observacion").GetString(), Is.EqualTo("Original"));
        Assert.That(item.GetProperty("idMaterial").GetInt32(), Is.EqualTo(2));
        Assert.That(item.GetProperty("idRequerimientoDetalle").GetInt32(), Is.EqualTo(detalle));
    }

    [Test]
    public async Task Actualizar_NullExplicito_LimpiaPartidaYObservacion()
    {
        var partida = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var id = await CrearAsync(partida: partida);
        var detalle = await IdDetalleAsync(id);
        var response = await PatchAsync(Ruta(id, detalle), """{"idPresupuestoDetalle":null,"observacion":null}""");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(item.GetProperty("idPresupuestoDetalle").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(item.GetProperty("observacion").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(item.GetProperty("cantidad").GetDecimal(), Is.EqualTo(1.25m));
    }

    [Test]
    public async Task Agregar_ConPartidaVigente_PersisteLaAsociacion()
    {
        var id = await CrearAsync();
        var partida = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        var response = await _client.PostAsJsonAsync(Ruta(id), new { idMaterial = 6, cantidad = 1m, idPresupuestoDetalle = partida });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(item.GetProperty("idPresupuestoDetalle").GetInt32(), Is.EqualTo(partida));
    }

    [Test]
    public async Task Eliminar_ConMasDeUnItem_Devuelve204YConservaElOtro()
    {
        var id = await CrearAsync(dosItems: true);
        var detalle = await IdDetalleAsync(id);
        var response = await _client.DeleteAsync(Ruta(id, detalle));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty);
        Assert.That(await CantidadAsync(id), Is.EqualTo(1));
    }

    [Test]
    public async Task Eliminar_UltimoItem_Devuelve409YNoLoElimina()
    {
        var id = await CrearAsync();
        var response = await _client.DeleteAsync(Ruta(id, await IdDetalleAsync(id)));
        await AssertErrorAsync(response, HttpStatusCode.Conflict, "REQUERIMIENTO_SIN_ITEMS");
        Assert.That(await CantidadAsync(id), Is.EqualTo(1));
    }

    [Test]
    public async Task Agregar_MaterialDuplicado_Devuelve409()
    {
        var id = await CrearAsync();
        var response = await _client.PostAsJsonAsync(Ruta(id), new { idMaterial = 2, cantidad = 1m });
        await AssertErrorAsync(response, HttpStatusCode.Conflict, "MATERIAL_DUPLICADO");
        Assert.That(await CantidadAsync(id), Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Agregar_ReferenciaInexistente_DevuelveConflictoDeDominio(bool partidaInexistente)
    {
        var id = await CrearAsync();
        var response = await _client.PostAsJsonAsync(Ruta(id), new
        {
            idMaterial = partidaInexistente ? 6 : int.MaxValue,
            cantidad = 1m,
            idPresupuestoDetalle = partidaInexistente ? (int?)int.MaxValue : null
        });
        await AssertErrorAsync(response, HttpStatusCode.Conflict,
            partidaInexistente ? "PARTIDA_REQUERIMIENTO_INVALIDA" : "REQUERIMIENTO_MATERIAL_INVALIDO");
        Assert.That(await CantidadAsync(id), Is.EqualTo(1));
    }

    [TestCase("POST")]
    [TestCase("PATCH")]
    [TestCase("DELETE")]
    public async Task Operacion_RequerimientoInexistente_Devuelve404(string method)
    {
        var response = await EnviarAsync(_client, method, int.MaxValue, 1);
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "REQUERIMIENTO_NO_ENCONTRADO");
    }

    [TestCase("PATCH")]
    [TestCase("DELETE")]
    public async Task Operacion_DetalleDeOtroRequerimiento_Devuelve404(string method)
    {
        var id = await CrearAsync();
        var otro = await CrearAsync();
        var detalleAjeno = await IdDetalleAsync(otro);
        var response = await EnviarAsync(_client, method, id, detalleAjeno);
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "REQUERIMIENTO_ITEM_NO_ENCONTRADO");
        Assert.That(await CantidadAsync(otro), Is.EqualTo(1));
    }

    [TestCase("POST")]
    [TestCase("PATCH")]
    [TestCase("DELETE")]
    public async Task Operacion_FueraDeAlmacen_Devuelve409(string method)
    {
        var id = await CrearAsync(enviar: false);
        var response = await EnviarAsync(_client, method, id, await IdDetalleAsync(id));
        await AssertErrorAsync(response, HttpStatusCode.Conflict, "ESTADO_REQUERIMIENTO_INVALIDO");
    }

    [TestCase("{}")]
    [TestCase("""{"cantidad":null}""")]
    [TestCase("""{"cantidad":0}""")]
    [TestCase("""{"cantidad":-1}""")]
    [TestCase("""{"cantidad":0.001}""")]
    [TestCase("""{"idMaterial":6}""")]
    [TestCase("""{"cantidad":2,"idMaterial":6}""")]
    [TestCase("""{"idPresupuestoDetalle":0}""")]
    [TestCase("""{"cantidad":"incorrecto"}""")]
    public async Task Actualizar_PayloadInvalido_Devuelve400SinCambios(string json)
    {
        var id = await CrearAsync();
        var response = await PatchAsync(Ruta(id, await IdDetalleAsync(id)), json);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await response.Content.ReadAsStringAsync());
        Assert.That(await DbHelpers.QueryScalarAsync<decimal>(
            "SELECT Cantidad FROM compras.RequerimientoDetalle WHERE IdRequerimiento=@id", new { id }), Is.EqualTo(1.25m));
    }

    [TestCase("""{"idMaterial":6}""")]
    [TestCase("""{"idMaterial":0,"cantidad":1}""")]
    [TestCase("""{"idMaterial":6,"cantidad":0.001}""")]
    public async Task Agregar_PayloadInvalido_Devuelve400(string json)
    {
        var id = await CrearAsync();
        var response = await _client.PostAsync(Ruta(id), new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await response.Content.ReadAsStringAsync());
        Assert.That(await CantidadAsync(id), Is.EqualTo(1));
    }

    [TestCase("POST", false, HttpStatusCode.Unauthorized)]
    [TestCase("PATCH", false, HttpStatusCode.Unauthorized)]
    [TestCase("DELETE", false, HttpStatusCode.Unauthorized)]
    [TestCase("POST", true, HttpStatusCode.Forbidden)]
    [TestCase("PATCH", true, HttpStatusCode.Forbidden)]
    [TestCase("DELETE", true, HttpStatusCode.Forbidden)]
    public async Task Operacion_SinAutorizacion_RechazaAntesDeConsultarDatos(string method, bool conToken, HttpStatusCode esperado)
    {
        using var client = GlobalSetupFixture.Factory.CreateClient();
        if (conToken)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                JwtTestTokenFactory.CrearToken(permisos: [Permisos.Requerimientos.EditarCantidadesAlmacen]));
        var response = await EnviarAsync(client, method, 999, 999);
        Assert.That(response.StatusCode, Is.EqualTo(esperado));
    }

    [Test]
    public async Task Agregar_SoloConPermisoNuevo_PermiteLaCapacidad()
    {
        var id = await CrearAsync();
        using var client = GlobalSetupFixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            JwtTestTokenFactory.CrearToken(idUsuario: SeedIds.AlmacenId,
                permisos: [Permisos.Requerimientos.GestionarItemsAlmacen]));
        var response = await client.PostAsJsonAsync(Ruta(id), new { idMaterial = 6, cantidad = 1m });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Flujo_DespuesDeEditarItems_ValidaYApruebaPorLosEndpointsExistentes()
    {
        var id = await CrearAsync();
        var agregar = await _client.PostAsJsonAsync(Ruta(id), new { idMaterial = 6, cantidad = 1m });
        Assert.That(agregar.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var validar = await _client.PostAsJsonAsync($"/api/compras/requerimientos/{id}/validacion-almacen",
            new { resultado = "Conforme", observacion = "Revisado" });
        Assert.That(validar.StatusCode, Is.EqualTo(HttpStatusCode.OK), await validar.Content.ReadAsStringAsync());
        var editar = await _client.PatchAsJsonAsync(Ruta(id, await IdDetalleAsync(id)), new { cantidad = 9m });
        Assert.That(editar.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var aprobar = await _client.PostAsJsonAsync($"/api/compras/requerimientos/{id}/aprobar", new { observacion = "Aprobado" });
        Assert.That(aprobar.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private async Task<int> CrearAsync(bool dosItems = false, bool enviar = true, int? partida = null)
    {
        var builder = RequerimientoBuilder.Nuevo().ConObservacion("Cabecera").ConItem(2, 1.25m, "Original", partida);
        if (dosItems) builder.ConItem(6, 2m);
        var id = await builder.CrearAsync(_client);
        if (enviar)
        {
            var response = await _client.PostAsJsonAsync($"/api/compras/requerimientos/{id}/enviar",
                new { observacion = (string?)null });
            Assert.That(response.IsSuccessStatusCode, Is.True, await response.Content.ReadAsStringAsync());
        }
        return id;
    }

    private static string Ruta(int id, int? detalle = null)
        => $"/api/compras/requerimientos/{id}/items" + (detalle.HasValue ? $"/{detalle}" : "");
    private static async Task<int> IdDetalleAsync(int id) => await DbHelpers.QueryScalarAsync<int>(
        "SELECT MIN(IdRequerimientoDetalle) FROM compras.RequerimientoDetalle WHERE IdRequerimiento=@id", new { id });
    private static async Task<int> CantidadAsync(int id) => await DbHelpers.QueryScalarAsync<int>(
        "SELECT COUNT(*) FROM compras.RequerimientoDetalle WHERE IdRequerimiento=@id", new { id });
    private Task<HttpResponseMessage> PatchAsync(string ruta, string json)
        => _client.PatchAsync(ruta, new StringContent(json, Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient client, string method, int id, int detalle)
        => method switch
        {
            "POST" => client.PostAsJsonAsync(Ruta(id), new { idMaterial = 6, cantidad = 1m }),
            "PATCH" => client.PatchAsJsonAsync(Ruta(id, detalle), new { cantidad = 2m }),
            _ => client.DeleteAsync(Ruta(id, detalle))
        };

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode esperado, string codigo)
    {
        Assert.That(response.StatusCode, Is.EqualTo(esperado), await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("error").GetString(), Is.EqualTo(codigo));
    }
}
