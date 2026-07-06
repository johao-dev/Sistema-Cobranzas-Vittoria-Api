using System.Net;
using System.Net.Http.Json;
using Cobranzas_Vittoria.Dtos.Auth;

namespace Cobranzas_Vittoria.Tests.Integration;

public class AuthControllerTests : IntegrationTestBase
{
    [Test]
    public async Task Login_ConCredencialesInvalidas_RetornaUnauthorized()
    {
        // Arrange
        var requestDto = new LoginRequestDto
        {
            UsuarioLogin = "usuario_hacker",
            Password = "passowrd_invalido"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", requestDto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}