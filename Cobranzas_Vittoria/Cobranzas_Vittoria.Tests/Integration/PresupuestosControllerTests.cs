using Cobranzas_Vittoria.Tests.Setup;
using System.Net;

namespace Cobranzas_Vittoria.Tests.Integration;

public class PresupuestosControllerTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PresupuestosControllerTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Test]
    public async Task GetByProyecto_CuandoExista_RetornaOk()
    {
        // Arrange
        int idProyecto = 1;

        // Act
        var response = await _client.GetAsync($"/api/contable/presupuesto/{idProyecto}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var contentType = response.Content.Headers.ContentType?.ToString();
        Assert.That(contentType, Does.Contain("application/json"));
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}