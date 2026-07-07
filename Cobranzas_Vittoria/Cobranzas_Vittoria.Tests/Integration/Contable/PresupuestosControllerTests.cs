using System.Net;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

public class PresupuestosControllerTests : IntegrationTestBase
{
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
}