using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

/// <summary>
/// Pruebas de integracion HTTP de <see cref="UsuarioController"/>.
///
/// Endpoints cubiertos:
///   GET    /api/seguridad/usuarios/{id}
///   GET    /api/seguridad/usuarios?activo=
///   POST   /api/seguridad/usuarios
///   PUT    /api/seguridad/usuarios/{id}
///   POST   /api/seguridad/usuarios/{id}/roles
///   DELETE /api/seguridad/usuarios/{id}/roles/{idRol}
///
/// Estas pruebas son ligeras: verifican codigos HTTP, forma del body y
/// persistencia basica. No cubren autorizacion/RBAC (pendiente de fase
/// futura).
/// </summary>
public class UsuarioControllerTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/seguridad/usuarios";

    /// <summary>
    /// Genera un sufijo unico para evitar colisiones entre tests.
    /// </summary>
    private static string NuevoLogin(string prefijo)
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        return $"{prefijo}-{guid}";
    }

    [Test]
    public async Task List_RetornaLosUsuariosDelSeed()
    {
        // Act
        var response = await _client.GetAsync(BaseUrl);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var wrapper = await response.Content.ReadFromJsonAsync<ListarUsuarioResponse>();
        Assert.That(wrapper, Is.Not.Null);
        var items = wrapper!.Usuarios.ToList();
        Assert.That(items.Count, Is.GreaterThanOrEqualTo(5));
        Assert.That(items.Any(u => u.UsuarioLogin == "admin"), Is.True);
    }

    [Test]
    public async Task List_ConFiltroActivoFalse_ExcluyeActivos()
    {
        // Arrange - crear un usuario activo y otro inactivo
        var loginActivo = NuevoLogin("activo");
        var loginInactivo = NuevoLogin("inactivo");

        var createActivo = await _client.PostAsJsonAsync(BaseUrl, new CreateUsuarioRequest(
            "Activo", "Usuario", $"{loginActivo}@local", loginActivo, "password"));
        createActivo.EnsureSuccessStatusCode();

        var createInactivo = await _client.PostAsJsonAsync(BaseUrl, new CreateUsuarioRequest(
            "Inactivo", "Usuario", $"{loginInactivo}@local", loginInactivo, "password"));
        createInactivo.EnsureSuccessStatusCode();
        var inactivoId = (await createInactivo.Content.ReadFromJsonAsync<UsuarioResponse>())!.IdUsuario;

        await DbHelpers.QueryScalarAsync<int>(
            "UPDATE seguridad.Usuario SET Activo = 0 WHERE IdUsuario = @id; SELECT @@ROWCOUNT;",
            new { id = inactivoId });

        // Act
        var response = await _client.GetAsync($"{BaseUrl}?activo=false");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var wrapper = await response.Content.ReadFromJsonAsync<ListarUsuarioResponse>();
        var items = wrapper!.Usuarios.ToList();
        Assert.That(items.TrueForAll(u => !u.Activo), Is.True);
        Assert.That(items.Exists(u => u.IdUsuario == inactivoId), Is.True);
        Assert.That(items.Exists(u => u.UsuarioLogin == loginActivo), Is.False);
    }

    [Test]
    public async Task Create_ConDatosValidos_Retorna201YPersisteEnBD()
    {
        // Arrange
        var login = NuevoLogin("create");
        var request = new CreateUsuarioRequest(
            "Juan",
            "Perez",
            $"{login}@local",
            login,
            "password");

        // Act
        var response = await _client.PostAsJsonAsync(BaseUrl, request);

        // Assert - HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        int id = body.GetProperty("idUsuario").GetInt32();
        Assert.That(id, Is.GreaterThan(0));
        Assert.That(JsonHelpers.GetString(body, "nombres"), Is.EqualTo(request.Nombres));
        Assert.That(JsonHelpers.GetString(body, "usuarioLogin"), Is.EqualTo(request.UsuarioLogin));

        // Assert - BD
        var loginEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT UsuarioLogin FROM seguridad.Usuario WHERE IdUsuario = @id",
            new { id });
        Assert.That(loginEnBd, Is.EqualTo(request.UsuarioLogin));
    }

    [Test]
    public async Task Create_ConCorreoDuplicado_Retorna422()
    {
        // Arrange
        var login = NuevoLogin("dup");
        var correo = $"{login}@local";
        var primero = await _client.PostAsJsonAsync(BaseUrl, new CreateUsuarioRequest(
            "Primero", "Usuario", correo, login, "password"));
        Assert.That(primero.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Act
        var response = await _client.PostAsJsonAsync(BaseUrl, new CreateUsuarioRequest(
            "Segundo", "Usuario", correo, NuevoLogin("dup2"), "password"));

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
    public async Task GetById_Existente_Retorna200ConUsuario()
    {
        // Arrange
        var login = NuevoLogin("get");
        var createResp = await _client.PostAsJsonAsync(BaseUrl, new CreateUsuarioRequest(
            "Get", "Usuario", $"{login}@local", login, "password"));
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        int id = createBody.GetProperty("idUsuario").GetInt32();

        // Act
        var response = await _client.GetAsync($"{BaseUrl}/{id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("idUsuario").GetInt32(), Is.EqualTo(id));
        Assert.That(JsonHelpers.GetString(body, "nombres"), Is.EqualTo("Get"));
    }

    [Test]
    public async Task GetById_Inexistente_Retorna404()
    {
        var response = await _client.GetAsync($"{BaseUrl}/9999999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaNoContentYSobreescribeEnBD()
    {
        // Arrange
        var login = NuevoLogin("upd");
        var createResp = await _client.PostAsJsonAsync(BaseUrl, new CreateUsuarioRequest(
            "Original", "Usuario", $"{login}@local", login, "password"));
        var created = await createResp.Content.ReadFromJsonAsync<UsuarioResponse>();
        Assert.That(created, Is.Not.Null);

        var dtoModificado = new UpdateUsuarioRequest(
            "Actualizado",
            null,
            null,
            null,
            null,
            false);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"{BaseUrl}/{created!.IdUsuario}",
            dtoModificado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var activoEnBd = await DbHelpers.QueryScalarAsync<bool>(
            "SELECT Activo FROM seguridad.Usuario WHERE IdUsuario = @id",
            new { id = created.IdUsuario });
        Assert.That(activoEnBd, Is.False);
    }

    [Test]
    public async Task Update_Inexistente_Retorna422()
    {
        var response = await _client.PutAsJsonAsync(
            $"{BaseUrl}/9999999",
            new UpdateUsuarioRequest("Nombre", "Apellido", null, null, null, null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            $"Body: {await response.Content.ReadAsStringAsync()}");
    }

    [Test]
    public async Task AsignarRoles_ConDatosValidos_Retorna204YPersisteEnBD()
    {
        // Arrange
        var login = NuevoLogin("roles");
        var createResp = await _client.PostAsJsonAsync(BaseUrl, new CreateUsuarioRequest(
            "Roles", "Usuario", $"{login}@local", login, "password"));
        var created = await createResp.Content.ReadFromJsonAsync<UsuarioResponse>();
        Assert.That(created, Is.Not.Null);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/{created!.IdUsuario}/roles",
            new AsignarRolesRequest(new[] { SeedIds.AdminId }));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var rolEnBd = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.UsuarioRol WHERE IdUsuario = @idUsuario AND IdRol = @idRol",
            new { idUsuario = created.IdUsuario, idRol = SeedIds.AdminId });
        Assert.That(rolEnBd, Is.EqualTo(1));
    }

    [Test]
    public async Task AsignarRoles_SinRoles_Retorna422()
    {
        // Arrange
        var login = NuevoLogin("noroles");
        var createResp = await _client.PostAsJsonAsync(BaseUrl, new CreateUsuarioRequest(
            "NoRoles", "Usuario", $"{login}@local", login, "password"));
        var created = await createResp.Content.ReadFromJsonAsync<UsuarioResponse>();
        Assert.That(created, Is.Not.Null);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/{created!.IdUsuario}/roles",
            new AsignarRolesRequest(Array.Empty<int>()));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
            $"Body: {await response.Content.ReadAsStringAsync()}");
    }

    [Test]
    public async Task QuitarRol_ConRolAsignado_Retorna204YRemueveEnBD()
    {
        // Arrange
        var login = NuevoLogin("quitrol");
        var createResp = await _client.PostAsJsonAsync(BaseUrl, new CreateUsuarioRequest(
            "QuitarRol", "Usuario", $"{login}@local", login, "password"));
        var created = await createResp.Content.ReadFromJsonAsync<UsuarioResponse>();
        Assert.That(created, Is.Not.Null);

        await _client.PostAsJsonAsync(
            $"{BaseUrl}/{created!.IdUsuario}/roles",
            new AsignarRolesRequest(new[] { SeedIds.AdminId }));

        // Act
        var response = await _client.DeleteAsync(
            $"{BaseUrl}/{created.IdUsuario}/roles/{SeedIds.AdminId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var rolEnBd = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.UsuarioRol WHERE IdUsuario = @idUsuario AND IdRol = @idRol",
            new { idUsuario = created.IdUsuario, idRol = SeedIds.AdminId });
        Assert.That(rolEnBd, Is.EqualTo(0));
    }
}
