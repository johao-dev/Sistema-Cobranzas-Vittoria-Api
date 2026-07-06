using System.Net;
using System.Net.Http.Json;
using Cobranzas_Vittoria.Dtos.Auth;
using Cobranzas_Vittoria.Tests.Setup;

namespace Cobranzas_Vittoria.Tests.Integration;

public class AuthControllerTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

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

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}