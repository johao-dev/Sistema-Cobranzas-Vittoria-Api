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
