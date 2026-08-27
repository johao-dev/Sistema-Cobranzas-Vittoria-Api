using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Auth;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

/// <summary>
/// Pruebas de AuthController (1 endpoint: POST /api/auth/login).
///
/// AuthController delega en AuthService.LoginAsync que valida que
/// UsuarioLogin y Password no esten vacios (Trim/IsNullOrWhiteSpace).
/// Si la consulta no retorna fila -> retorna null -> 401.
///
/// Repositorio filtra por UsuarioLogin y PasswordHash directamente
/// (comparacion plana, sin hash real: los PasswordHash del seed son
/// texto plano tipo "admin123", "ingeniero", etc.).
///
/// Seed (V1_1_0__SeedData.sql):
///   admin    / admin123    (Id=1, Rol=ADMIN)
///   ingeniero/ ingeniero   (Id=2, Rol=CONTABLE)
///   almacen  / almacen     (Id=3)
///   contable / contable    (Id=4)
///
/// Respuestas:
///   - 200: { idUsuario, nombres, apellidos, correo, usuarioLogin, nombre }
///   - 401: { message: "Usuario o contraseña incorrectos." }
///   - 400: ModelState invalido (body vacio, JSON malformado)
/// </summary>
public class AuthControllerTests : IntegrationTestBase
{
    [Test]
    public async Task Login_ConCredencialesValidasAdmin_RetornaOkYUsuario()
    {
        // Arrange
        var dto = new LoginRequestDto
        {
            UsuarioLogin = "admin",
            Password = "admin123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");

        // Assert - 2: payload completo del usuario
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetInt32(body, "idUsuario"), Is.EqualTo(SeedIds.AdminId));
        Assert.That(JsonHelpers.GetString(body, "usuarioLogin"), Is.EqualTo("admin"));
        Assert.That(JsonHelpers.GetString(body, "nombres"), Is.EqualTo("Administrador"));
        Assert.That(JsonHelpers.GetString(body, "nombre"), Is.Not.Empty);
    }

    [Test]
    public async Task Login_ConPasswordIncorrecto_RetornaUnauthorized()
    {
        // Arrange - usuario valido del seed, password equivocado
        var dto = new LoginRequestDto
        {
            UsuarioLogin = "admin",
            Password = "password-equivocado"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "message"), Is.EqualTo("Usuario o contraseña incorrectos."));
    }

    [Test]
    public async Task Login_ConUsuarioInexistente_RetornaUnauthorized()
    {
        // Arrange
        var dto = new LoginRequestDto
        {
            UsuarioLogin = "usuario_que_no_existe",
            Password = "cualquier"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "message"), Is.EqualTo("Usuario o contraseña incorrectos."));
    }

    [Test]
    public async Task Login_ConUsuarioVacio_RetornaUnauthorizedPorValidacionDelService()
    {
        // Arrange - el service hace IsNullOrWhiteSpace(usuario) y retorna null -> 401
        var dto = new LoginRequestDto
        {
            UsuarioLogin = "   ",
            Password = "algo"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_ConBodyVacio_RetornaBadRequest()
    {
        // Arrange - body vacio invalida el ModelState del DTO
        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/login", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
