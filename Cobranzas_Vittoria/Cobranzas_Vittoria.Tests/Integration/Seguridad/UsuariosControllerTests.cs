using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Seguridad;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

/// <summary>
/// Pruebas de UsuariosController.
///   GET    /api/seguridad/usuarios?activo=                  -> List
///   GET    /api/seguridad/usuarios/{id}                     -> Get (404 si no existe)
///   POST   /api/seguridad/usuarios                          -> Upsert (insert)
///   PUT    /api/seguridad/usuarios/{id}                     -> Upsert (update)
///   POST   /api/seguridad/usuarios/{id}/roles               -> AssignRole
///   DELETE /api/seguridad/usuarios/{id}/roles/{idRol}       -> RemoveRole
///
/// SP seguridad.usp_Usuario_Upsert:
///   * THROW 50001 si Nombres vacío
///   * THROW 50002 si UsuarioLogin vacío
///   * THROW 50003 si UsuarioLogin duplicado en INSERT
///   * THROW 50004 si UsuarioLogin duplicado en UPDATE (otro Id)
/// SP seguridad.usp_UsuarioRol_Asignar:
///   * THROW 50005 si Usuario no existe
///   * THROW 50006 si Rol no existe
/// SP seguridad.usp_UsuarioRol_Quitar:
///   * DELETE directo (idempotente: si la fila no existe, no pasa nada)
/// </summary>
public class UsuariosControllerTests : IntegrationTestBase
{
    [Test]
    public async Task List_RetornaLosUsuariosDelSeed()
    {
        // Act
        var response = await _client.GetAsync("/api/seguridad/usuarios");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var items = await response.Content.ReadFromJsonAsync<List<UsuarioUpsertDto>>();
        Assert.That(items, Is.Not.Null);
        // El seed mete 4 usuarios (admin, ingeniero, almacen, contable)
        Assert.That(items!.Count, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public async Task GetById_ConIdExistente_RetornaOkYUsuarioCompletoConRoles()
    {
        // Arrange - tomamos el admin del seed (Id=1)
        int id = SeedIds.AdminId;

        // Act
        var response = await _client.GetAsync($"/api/seguridad/usuarios/{id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // El repository devuelve tuple (usuario, roles). El JSON es:
        //   { "usuario": { "idUsuario": 1, ... }, "roles": [ ... ] }
        Assert.That(body.TryGetProperty("usuario", out var usuario), Is.True);
        Assert.That(usuario.GetProperty("idUsuario").GetInt32(), Is.EqualTo(id));
        Assert.That(usuario.GetProperty("usuarioLogin").GetString(), Is.EqualTo("admin"));
        Assert.That(body.TryGetProperty("roles", out var roles), Is.True);
        // admin tiene rol ADMIN
        Assert.That(roles.ValueKind, Is.EqualTo(JsonValueKind.Array));
    }

    [Test]
    public async Task GetById_ConIdInexistente_RetornaNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/seguridad/usuarios/9999999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Create_ConDatosValidos_RetornaOkAsignaIdYPersisteEnBD()
    {
        // Arrange
        var dto = new UsuarioUpsertDto
        {
            Nombres = "Test",
            Apellidos = "User",
            Correo = "test@user.com",
            UsuarioLogin = $"user_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "hash-prueba-no-se-valida",  // el controller no hashea
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/seguridad/usuarios", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        int id = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idUsuario").GetInt32();
        Assert.That(id, Is.GreaterThan(0));

        // Assert - 2: BD
        var loginEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT UsuarioLogin FROM seguridad.Usuario WHERE IdUsuario = @id",
            new { id });
        Assert.That(loginEnBd, Is.EqualTo(dto.UsuarioLogin));
    }

    [Test]
    public async Task Create_ConUsuarioLoginDuplicado_Retorna5xxPorThrowDelSP()
    {
        // Arrange - usamos el admin del seed que ya existe
        var dto = new UsuarioUpsertDto
        {
            Nombres = "Otro",
            Apellidos = "Admin",
            UsuarioLogin = "admin",  // ya existe (Id=1)
            PasswordHash = "x",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/seguridad/usuarios", dto);

        // Assert
        // El SP hace THROW 50003 (Ya existe un usuario con ese login).
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaOkYSobreescribeEnBD()
    {
        // Arrange - creamos un usuario
        var dtoOriginal = new UsuarioUpsertDto
        {
            Nombres = "User Original",
            UsuarioLogin = $"user_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "hash1",
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/seguridad/usuarios", dtoOriginal);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idUsuario").GetInt32();

        var dtoModificado = new UsuarioUpsertDto
        {
            IdUsuario = id,
            Nombres = "User Modificado",
            Apellidos = "Apellido Test",
            Correo = "mod@user.com",
            UsuarioLogin = dtoOriginal.UsuarioLogin,
            PasswordHash = dtoOriginal.PasswordHash,
            Activo = false
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/seguridad/usuarios/{id}", dtoModificado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var activoEnBd = await DbHelpers.QueryScalarAsync<bool>(
            "SELECT Activo FROM seguridad.Usuario WHERE IdUsuario = @id",
            new { id });
        Assert.That(activoEnBd, Is.False);
    }

    [Test]
    public async Task AssignRole_AUsuarioYRolExistentes_PersisteAsignacionEnBD()
    {
        // Arrange
        var dto = new UsuarioUpsertDto
        {
            Nombres = "User Test",
            UsuarioLogin = $"user_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/seguridad/usuarios", dto);
        int idUsuario = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idUsuario").GetInt32();

        // Act - asignamos el rol CONTABLE (Id=4 del seed)
        var assignResp = await _client.PostAsJsonAsync(
            $"/api/seguridad/usuarios/{idUsuario}/roles",
            new UsuarioRolDto { IdUsuario = idUsuario, IdRol = SeedIds.ContableId });

        // Assert - 1: HTTP
        Assert.That(assignResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert - 2: BD
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.UsuarioRol WHERE IdUsuario = @u AND IdRol = @r",
            new { u = idUsuario, r = SeedIds.ContableId });
        Assert.That(count, Is.EqualTo(1), "La asignación debe haber persistido en UsuarioRol.");
    }

    [Test]
    public async Task AssignRole_AUsuarioInexistente_Retorna5xxPorThrowDelSP()
    {
        // Act
        // El SP hace THROW 50005 si el usuario no existe.
        var response = await _client.PostAsJsonAsync(
            "/api/seguridad/usuarios/9999999/roles",
            new UsuarioRolDto { IdUsuario = 9999999, IdRol = SeedIds.ContableId });

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    public async Task RemoveRole_DeAsignacionExistente_RetornaOkYEliminaDeBD()
    {
        // Arrange
        // 1) Creamos un usuario
        var dto = new UsuarioUpsertDto
        {
            Nombres = "User Test",
            UsuarioLogin = $"user_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "x",
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/seguridad/usuarios", dto);
        int idUsuario = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idUsuario").GetInt32();

        // 2) Le asignamos el rol ALMACEN
        var assignResp = await _client.PostAsJsonAsync(
            $"/api/seguridad/usuarios/{idUsuario}/roles",
            new UsuarioRolDto { IdUsuario = idUsuario, IdRol = SeedIds.AlmacenId });
        Assert.That(assignResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act
        var response = await _client.DeleteAsync(
            $"/api/seguridad/usuarios/{idUsuario}/roles/{SeedIds.AlmacenId}");

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert - 2: BD - la asignación debe haberse eliminado
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.UsuarioRol WHERE IdUsuario = @u AND IdRol = @r",
            new { u = idUsuario, r = SeedIds.AlmacenId });
        Assert.That(count, Is.EqualTo(0), "RemoveRole debe eliminar la fila de UsuarioRol.");
    }

    [Test]
    public async Task RemoveRole_DeAsignacionInexistente_RetornaOkYNoLanzaError()
    {
        // El SP de Remove es un DELETE directo, no valida existencia.
        // Eliminar algo que no existe debe ser idempotente y devolver 200.

        // Act
        var response = await _client.DeleteAsync(
            $"/api/seguridad/usuarios/{SeedIds.AdminId}/roles/9999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
