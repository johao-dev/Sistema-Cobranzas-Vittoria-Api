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
///   POST   /api/seguridad/permisos
///   PUT    /api/seguridad/permisos/{id}
///   DELETE /api/seguridad/permisos/{id}
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
        var guid = Guid.NewGuid().ToString("N");
        var codigo = $"{prefijo}-{guid}";
        const int limite = 128;

        if (codigo.Length <= limite)
            return codigo;

        return codigo.Substring(0, limite);
    }

    [Test]
    public async Task Create_ConDatosValidos_Retorna201YPersisteEnBD()
    {
        // Arrange
        var request = new CreatePermisoRequest(
            NuevoCodigo("create"),
            "Permiso controller",
            "Desc");

        // Act
        var response = await _client.PostAsJsonAsync(BaseUrl, request);

        // Assert - HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        int id = body.GetProperty("idPermiso").GetInt32();
        Assert.That(id, Is.GreaterThan(0));
        Assert.That(JsonHelpers.GetString(body, "codigo"), Is.EqualTo(request.Codigo));
        Assert.That(JsonHelpers.GetString(body, "nombre"), Is.EqualTo(request.Nombre));

        // Assert - BD
        var nombreEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Nombre FROM seguridad.Permiso WHERE IdPermiso = @id",
            new { id });
        Assert.That(nombreEnBd, Is.EqualTo(request.Nombre));
    }

    [Test]
    public async Task Create_ConCodigoDuplicado_Retorna422()
    {
        // Arrange
        var codigo = NuevoCodigo("dup");
        var primero = await _client.PostAsJsonAsync(BaseUrl, new CreatePermisoRequest(codigo, "Primero", ""));
        Assert.That(primero.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Act
        var response = await _client.PostAsJsonAsync(BaseUrl, new CreatePermisoRequest(codigo, "Segundo", ""));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            $"Body: {await response.Content.ReadAsStringAsync()}");
    }

    [Test]
    public async Task Create_ConBodyVacio_Retorna400()
    {
        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(BaseUrl, content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetById_Existente_Retorna200ConPermiso()
    {
        // Arrange
        var createResp = await _client.PostAsJsonAsync(BaseUrl,
            new CreatePermisoRequest(NuevoCodigo("get"), "Get", ""));
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        int id = createBody.GetProperty("idPermiso").GetInt32();

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
        var codigo = NuevoCodigo("list");
        await _client.PostAsJsonAsync(BaseUrl, new CreatePermisoRequest(codigo, "List", ""));

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
        var codigoActivo = NuevoCodigo("active");
        var codigoInactivo = NuevoCodigo("inactive");
        var createActivo = await _client.PostAsJsonAsync(BaseUrl,
            new CreatePermisoRequest(codigoActivo, "Activo", ""));
        var createInactivo = await _client.PostAsJsonAsync(BaseUrl,
            new CreatePermisoRequest(codigoInactivo, "Inactivo", ""));
        var inactivoId = (await createInactivo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idPermiso").GetInt32();

        // Desactivar via BD para no depender del endpoint de update
        await DbHelpers.QueryScalarAsync<int>(
            "UPDATE seguridad.Permiso SET Activo = 0 WHERE IdPermiso = @id; SELECT @@ROWCOUNT;",
            new { id = inactivoId });

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
        var createResp = await _client.PostAsJsonAsync(BaseUrl,
            new CreatePermisoRequest(NuevoCodigo("upd"), "Original", "Desc"));
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        int id = createBody.GetProperty("idPermiso").GetInt32();

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
    public async Task Delete_Existente_Retorna204YEliminaEnBD()
    {
        // Arrange
        var createResp = await _client.PostAsJsonAsync(BaseUrl,
            new CreatePermisoRequest(NuevoCodigo("del"), "Delete", ""));
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        int id = createBody.GetProperty("idPermiso").GetInt32();

        // Act
        var response = await _client.DeleteAsync($"{BaseUrl}/{id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var countEnBd = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.Permiso WHERE IdPermiso = @id",
            new { id });
        Assert.That(countEnBd, Is.EqualTo(0));
    }

    [Test]
    public async Task Delete_Inexistente_Retorna422()
    {
        var response = await _client.DeleteAsync($"{BaseUrl}/9999999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            $"Body: {await response.Content.ReadAsStringAsync()}");
    }
}
