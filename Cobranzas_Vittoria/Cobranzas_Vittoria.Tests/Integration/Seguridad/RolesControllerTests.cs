using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Seguridad;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

/// <summary>
/// Pruebas de RolesController.
///   GET  /api/seguridad/roles?activo=   -> List
///   POST /api/seguridad/roles          -> Upsert (insert)
///   PUT  /api/seguridad/roles/{id}     -> Upsert (update)
///
/// Notas:
///   * El controller NO expone DELETE ni GET /{id}, así que 404 no aplica.
///   * El SP seguridad.usp_Rol_Upsert existe pero el controller no lo valida inline;
///     si el SP hace THROW, ApiExceptionMiddleware lo traduce a 500.
///   * El seed mete 4 roles (ADMIN, INGENIERO, ALMACEN, CONTABLE) en Id=1,2,3,4.
/// </summary>
public class RolesControllerTests : IntegrationTestBase
{
    [Test]
    public async Task List_RetornaLosRolesDelSeed()
    {
        // Act
        var response = await _client.GetAsync("/api/seguridad/roles");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var items = await response.Content.ReadFromJsonAsync<List<RolUpsertDto>>();
        Assert.That(items, Is.Not.Null);
        // El seed mete 4 roles
        Assert.That(items!.Count, Is.GreaterThanOrEqualTo(4));
        // El seed mete los roles como "Administrador", "almacen", "ingeniero", "contable".
        // Verificamos que el rol "Administrador" está presente.
        Assert.That(items!.Any(r => r.NombreRol == "Administrador"), Is.True);
    }

    [Test]
    public async Task List_ConFiltroActivoFalse_ExcluyeActivos()
    {
        // Arrange - creamos un rol inactivo
        var inactivo = new RolUpsertDto
        {
            NombreRol = $"ROL-INACTIVO-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = false
        };
        await _client.PostAsJsonAsync("/api/seguridad/roles", inactivo);

        // Act
        var response = await _client.GetAsync("/api/seguridad/roles?activo=false");
        var items = await response.Content.ReadFromJsonAsync<List<RolUpsertDto>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items!.All(r => !r.Activo), Is.True);
        Assert.That(items!.Any(r => r.NombreRol == inactivo.NombreRol), Is.True);
    }

    [Test]
    public async Task Create_ConNombreValido_RetornaOkAsignaIdYPersisteEnBD()
    {
        // Arrange
        var dto = new RolUpsertDto
        {
            NombreRol = $"ROL-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/seguridad/roles", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        int id = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idRol").GetInt32();
        Assert.That(id, Is.GreaterThan(0));

        // Assert - 2: BD
        var nombreEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT NombreRol FROM seguridad.Rol WHERE IdRol = @id",
            new { id });
        Assert.That(nombreEnBd, Is.EqualTo(dto.NombreRol));
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaOkYSobreescribeEnBD()
    {
        // Arrange
        var dtoOriginal = new RolUpsertDto
        {
            NombreRol = $"ROL-UPD-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/seguridad/roles", dtoOriginal);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idRol").GetInt32();

        var dtoModificado = new RolUpsertDto
        {
            IdRol = id,
            NombreRol = dtoOriginal.NombreRol,
            Activo = false
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/seguridad/roles/{id}", dtoModificado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var activoEnBd = await DbHelpers.QueryScalarAsync<bool>(
            "SELECT Activo FROM seguridad.Rol WHERE IdRol = @id",
            new { id });
        Assert.That(activoEnBd, Is.False);
    }
}
