using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

/// <summary>
/// Pruebas de integracion HTTP de <see cref="PermisoController"/>.
///
/// Endpoints cubiertos:
///   GET    /api/seguridad/permisos/{id}
///   GET    /api/seguridad/permisos?activo=
///   PUT    /api/seguridad/permisos/{id}
///   PATCH  /api/seguridad/permisos/{id}/enable
///   PATCH  /api/seguridad/permisos/{id}/disable
///
/// Estas pruebas son ligeras: verifican codigos HTTP, forma del body y
/// persistencia basica. No cubren autorizacion/RBAC (pendiente de fase
/// futura).
/// </summary>
public class PermisoControllerTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/seguridad/permisos";

    /// <summary>
    /// La columna <c>seguridad.Permiso.Codigo</c> es <c>nvarchar(128)</c>,
    /// por lo que todos los códigos generados en tests deben respetar ese límite.
    /// </summary>
    private static string NuevoCodigo(string prefijo)
    {
        string guid = Guid.NewGuid().ToString("N");
        string codigo = $"{prefijo}-{guid}";
        const int limite = 128;

        if (codigo.Length <= limite)
            return codigo;

        return codigo.Substring(0, limite);
    }

    [Test]
    public async Task GetById_Existente_Retorna200ConPermiso()
    {
        // Arrange
        int id = await PermisoTestData.InsertarAsync(NuevoCodigo("get"), "Get");

        // Act
        var response = await _client.GetAsync($"{BaseUrl}/{id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("idPermiso").GetInt32(), Is.EqualTo(id));
        Assert.That(JsonHelpers.GetString(body, "nombre"), Is.EqualTo("Get"));
    }

    [Test]
    public async Task GetById_Inexistente_Retorna404()
    {
        var response = await _client.GetAsync($"{BaseUrl}/9999999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task List_RetornaPermisosCreados()
    {
        // Arrange
        string codigo = NuevoCodigo("list");
        await PermisoTestData.InsertarAsync(codigo, "List");

        // Act
        var response = await _client.GetAsync(BaseUrl);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("permisos").EnumerateArray().ToList();
        Assert.That(items, Is.Not.Empty);
        Assert.That(items.Exists(p => JsonHelpers.GetString(p, "codigo") == codigo), Is.True);
    }

    [Test]
    public async Task List_ConFiltroActivoFalse_RetornaSoloInactivos()
    {
        // Arrange - crear activo e inactivo
        string codigoActivo = NuevoCodigo("active");
        string codigoInactivo = NuevoCodigo("inactive");
        await PermisoTestData.InsertarAsync(codigoActivo, "Activo");
        await PermisoTestData.InsertarAsync(codigoInactivo, "Inactivo", activo: false);

        // Act
        var response = await _client.GetAsync($"{BaseUrl}?activo=false");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("permisos").EnumerateArray().ToList();
        Assert.That(items.TrueForAll(p => p.GetProperty("activo").GetBoolean() == false), Is.True);
        Assert.That(items.Exists(p => JsonHelpers.GetString(p, "codigo") == codigoInactivo), Is.True);
        Assert.That(items.Exists(p => JsonHelpers.GetString(p, "codigo") == codigoActivo), Is.False);
    }

    [Test]
    public async Task Update_Existente_Retorna204YActualizaEnBD()
    {
        // Arrange
        int id = await PermisoTestData.InsertarAsync(NuevoCodigo("upd"), "Original", "Desc");

        // Act
        var response = await _client.PutAsJsonAsync($"{BaseUrl}/{id}",
            new UpdatePermisoRequest("Actualizado", "Nueva desc"));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var nombreEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Nombre FROM seguridad.Permiso WHERE IdPermiso = @id",
            new { id });
        Assert.That(nombreEnBd, Is.EqualTo("Actualizado"));
    }

    [Test]
    public async Task Update_Inexistente_Retorna422()
    {
        var response = await _client.PutAsJsonAsync($"{BaseUrl}/9999999",
            new UpdatePermisoRequest("Nombre", "Desc"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            $"Body: {await response.Content.ReadAsStringAsync()}");
    }

    [TestCase("enable", true)]
    [TestCase("disable", false)]
    public async Task CambiarEstado_EstadoOpuesto_Retorna204YActualizaConsultas(string accion, bool activo)
    {
        string codigo = NuevoCodigo(accion);
        int idPermiso = await PermisoTestData.InsertarAsync(codigo, "Original", "Descripcion", !activo);
        PermisoResponse antes = (await _client.GetFromJsonAsync<PermisoResponse>($"{BaseUrl}/{idPermiso}"))!;

        HttpResponseMessage response = await _client.PatchAsync($"{BaseUrl}/{idPermiso}/{accion}", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent),
            await response.Content.ReadAsStringAsync());

        PermisoResponse despues = (await _client.GetFromJsonAsync<PermisoResponse>($"{BaseUrl}/{idPermiso}"))!;
        ListarPermisoResponse listado = (await _client.GetFromJsonAsync<ListarPermisoResponse>(
            $"{BaseUrl}?activo={activo.ToString().ToLowerInvariant()}"))!;

        Assert.Multiple(() =>
        {
            Assert.That(despues.Activo, Is.EqualTo(activo));
            Assert.That(despues.UsuarioModificacion, Is.EqualTo(JwtTestTokenFactory.UsuarioLogin));
            Assert.That(despues.FechaModificacion, Is.Not.Null);
            Assert.That(despues.Codigo, Is.EqualTo(antes.Codigo));
            Assert.That(despues.Nombre, Is.EqualTo(antes.Nombre));
            Assert.That(despues.Descripcion, Is.EqualTo(antes.Descripcion));
            Assert.That(despues.FechaCreacion, Is.EqualTo(antes.FechaCreacion));
            Assert.That(despues.UsuarioCreacion, Is.EqualTo(antes.UsuarioCreacion));
            Assert.That(listado.Permisos.Any(permiso => permiso.IdPermiso == idPermiso), Is.True);
        });
    }

    [TestCase("enable", true, "PERMISO_YA_HABILITADO")]
    [TestCase("disable", false, "PERMISO_YA_DESHABILITADO")]
    public async Task CambiarEstado_EstadoRepetido_Retorna422SinModificarAuditoria(
        string accion, bool activo, string codigoError)
    {
        int idPermiso = await PermisoTestData.InsertarAsync(NuevoCodigo(accion), "Original", activo: !activo);
        HttpResponseMessage primero = await _client.PatchAsync($"{BaseUrl}/{idPermiso}/{accion}", null);
        Assert.That(primero.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        PermisoResponse antes = (await _client.GetFromJsonAsync<PermisoResponse>($"{BaseUrl}/{idPermiso}"))!;

        HttpResponseMessage repetido = await _client.PatchAsync($"{BaseUrl}/{idPermiso}/{accion}", null);

        await VerificarErrorNegocioAsync(repetido, codigoError);
        PermisoResponse despues = (await _client.GetFromJsonAsync<PermisoResponse>($"{BaseUrl}/{idPermiso}"))!;
        Assert.That(despues, Is.EqualTo(antes));
    }

    [TestCase("enable")]
    [TestCase("disable")]
    public async Task CambiarEstado_PermisoInexistente_Retorna422(string accion)
    {
        HttpResponseMessage response = await _client.PatchAsync($"{BaseUrl}/{int.MaxValue}/{accion}", null);

        await VerificarErrorNegocioAsync(response, "PERMISO_NO_ENCONTRADO");
    }

    [TestCase("enable", 0)]
    [TestCase("enable", -1)]
    [TestCase("disable", 0)]
    [TestCase("disable", -1)]
    public async Task CambiarEstado_IdInvalido_Retorna400(string accion, int idPermiso)
    {
        HttpResponseMessage response = await _client.PatchAsync($"{BaseUrl}/{idPermiso}/{accion}", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("enable", true)]
    [TestCase("disable", false)]
    public async Task Update_DespuesDeCambiarEstado_ConservaEstado(string accion, bool activo)
    {
        int idPermiso = await PermisoTestData.InsertarAsync(NuevoCodigo(accion), "Original", activo: !activo);
        HttpResponseMessage cambioEstado = await _client.PatchAsync($"{BaseUrl}/{idPermiso}/{accion}", null);
        Assert.That(cambioEstado.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        HttpResponseMessage actualizacion = await _client.PutAsJsonAsync($"{BaseUrl}/{idPermiso}",
            new UpdatePermisoRequest("Actualizado", "Descripcion nueva"));

        Assert.That(actualizacion.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        PermisoResponse permiso = (await _client.GetFromJsonAsync<PermisoResponse>($"{BaseUrl}/{idPermiso}"))!;
        Assert.That(permiso.Activo, Is.EqualTo(activo));
        Assert.That(permiso.Nombre, Is.EqualTo("Actualizado"));
    }

    private static async Task VerificarErrorNegocioAsync(HttpResponseMessage response, string codigoError)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            await response.Content.ReadAsStringAsync());
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("errores").EnumerateArray().Single().GetProperty("codigoError").GetString(),
            Is.EqualTo(codigoError));
    }

    [Test]
    public async Task Post_NoDisponible_Retorna405SinCrearPermiso()
    {
        string codigo = NuevoCodigo("post-retirado");

        HttpResponseMessage response = await _client.PostAsJsonAsync(BaseUrl,
            new { Codigo = codigo, Nombre = "Alta retirada", Descripcion = "" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));

        int cantidad = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.Permiso WHERE Codigo = @codigo", new { codigo });
        Assert.That(cantidad, Is.Zero);
    }

    [Test]
    public async Task Delete_NoDisponible_Retorna405YConservaPermiso()
    {
        int id = await PermisoTestData.InsertarAsync(NuevoCodigo("delete-retirado"), "Baja retirada");

        HttpResponseMessage response = await _client.DeleteAsync($"{BaseUrl}/{id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));

        int cantidad = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.Permiso WHERE IdPermiso = @id", new { id });
        Assert.That(cantidad, Is.EqualTo(1));
    }
}
