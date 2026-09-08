using System.Net;
using System.Net.Http.Json;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

/// <summary>
/// Pruebas de RolController usando el nuevo contrato del modulo Seguridad.
///   GET  /api/seguridad/roles?activo=   -> List
///   POST /api/seguridad/roles          -> Create
///   PUT  /api/seguridad/roles/{id}     -> Update
///
/// Notas:
///   * El seed mete 4 roles (ADMIN, INGENIERO, ALMACEN, CONTABLE) en Id=1,2,3,4.
/// </summary>
public class RolControllerTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/seguridad/roles";

    [Test]
    public async Task List_RetornaLosRolesDelSeed()
    {
        // Act
        var response = await _client.GetAsync("/api/seguridad/roles");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var wrapper = await response.Content.ReadFromJsonAsync<ListarRolResponse>();
        Assert.That(wrapper, Is.Not.Null);
        var items = wrapper!.Roles.ToList();
        // El seed mete 4 roles
        Assert.That(items.Count, Is.GreaterThanOrEqualTo(4));
        // El seed mete los roles como "Administrador", "almacen", "ingeniero", "contable".
        // Verificamos que el rol "Administrador" está presente.
        Assert.That(items.Any(r => r.Nombre == "Administrador"), Is.True);
    }

    [Test]
    public async Task List_ConFiltroActivoFalse_ExcluyeActivos()
    {
        // Arrange - creamos un rol y luego lo desactivamos
        var inactivo = new CreateRolRequest(
            $"ROL-INACTIVO-{Guid.NewGuid():N}".Substring(0, 20),
            "Rol inactivo de prueba");

        var createResponse = await _client.PostAsJsonAsync("/api/seguridad/roles", inactivo);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<RolResponse>();
        Assert.That(created, Is.Not.Null);

        var disableRequest = new UpdateRolRequest(created!.Nombre, created.Descripcion, false);
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/seguridad/roles/{created.IdRol}",
            disableRequest);
        updateResponse.EnsureSuccessStatusCode();

        // Act
        var response = await _client.GetAsync("/api/seguridad/roles?activo=false");
        var wrapper = await response.Content.ReadFromJsonAsync<ListarRolResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var items = wrapper!.Roles.ToList();
        Assert.That(items.All(r => !r.Activo), Is.True);
        Assert.That(items.Any(r => r.Nombre == inactivo.Nombre), Is.True);
    }

    [Test]
    public async Task Create_ConNombreValido_RetornaCreatedAsignaIdYPersisteEnBD()
    {
        // Arrange
        CreateRolRequest dto = new CreateRolRequest(
            $"ROL-{Guid.NewGuid():N}".Substring(0, 20),
            "ROL");

        // Act
        var response = await _client.PostAsJsonAsync("/api/seguridad/roles", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<RolResponse>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.IdRol, Is.GreaterThan(0));

        // Assert - 2: BD
        var nombreEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Nombre FROM seguridad.Rol WHERE IdRol = @id",
            new { id = created.IdRol });
        Assert.That(nombreEnBd, Is.EqualTo(dto.Nombre));
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaNoContentYSobreescribeEnBD()
    {
        // Arrange
        var dtoOriginal = new CreateRolRequest(
            $"ROL-UPD-{Guid.NewGuid():N}".Substring(0, 20),
            "Rol para actualizar");

        var createResp = await _client.PostAsJsonAsync("/api/seguridad/roles", dtoOriginal);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<RolResponse>();
        Assert.That(created, Is.Not.Null);

        var dtoModificado = new UpdateRolRequest(
            dtoOriginal.Nombre,
            null,
            false);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/seguridad/roles/{created!.IdRol}",
            dtoModificado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var activoEnBd = await DbHelpers.QueryScalarAsync<bool>(
            "SELECT Activo FROM seguridad.Rol WHERE IdRol = @id",
            new { id = created.IdRol });
        Assert.That(activoEnBd, Is.False);
    }

    [Test]
    public async Task AsignarPermisos_ConDatosValidos_Retorna204YPersisteSinDuplicados()
    {
        RolResponse rol = await CrearRolAsync("ASIG-PERM");
        PermisoResponse permiso = await CrearPermisoAsync("asig-perm");

        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/{rol.IdRol}/permisos",
            new AsignarPermisosRequest(new[] { permiso.IdPermiso, permiso.IdPermiso }));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var asociaciones = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.PermisoRol WHERE IdRol = @idRol AND IdPermiso = @idPermiso",
            new { idRol = rol.IdRol, idPermiso = permiso.IdPermiso });
        Assert.That(asociaciones, Is.EqualTo(1));
    }

    [Test]
    public async Task AsignarPermisos_SinPermisos_Retorna422()
    {
        RolResponse rol = await CrearRolAsync("SIN-PERM");

        var response = await _client.PostAsJsonAsync(
            $"{BaseUrl}/{rol.IdRol}/permisos",
            new AsignarPermisosRequest(Array.Empty<int>()));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    [Test]
    public async Task QuitarPermiso_ConAsociacionExistente_Retorna204YLaElimina()
    {
        RolResponse rol = await CrearRolAsync("QUIT-PERM");
        PermisoResponse permiso = await CrearPermisoAsync("quit-perm");
        await _client.PostAsJsonAsync(
            $"{BaseUrl}/{rol.IdRol}/permisos",
            new AsignarPermisosRequest(new[] { permiso.IdPermiso }));

        var response = await _client.DeleteAsync(
            $"{BaseUrl}/{rol.IdRol}/permisos/{permiso.IdPermiso}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var asociaciones = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.PermisoRol WHERE IdRol = @idRol AND IdPermiso = @idPermiso",
            new { idRol = rol.IdRol, idPermiso = permiso.IdPermiso });
        Assert.That(asociaciones, Is.Zero);
    }

    private async Task<RolResponse> CrearRolAsync(string prefijo)
    {
        var request = new CreateRolRequest($"{prefijo}-{Guid.NewGuid():N}"[..20], "Rol de prueba");
        var response = await _client.PostAsJsonAsync(BaseUrl, request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RolResponse>())!;
    }

    private async Task<PermisoResponse> CrearPermisoAsync(string prefijo)
    {
        var request = new CreatePermisoRequest(
            $"{prefijo}-{Guid.NewGuid():N}",
            "Permiso de prueba",
            "");
        var response = await _client.PostAsJsonAsync("/api/seguridad/permisos", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PermisoResponse>())!;
    }
}
