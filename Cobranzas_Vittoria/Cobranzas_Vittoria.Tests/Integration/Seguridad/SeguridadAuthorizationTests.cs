using System.Net;
using System.Net.Http.Headers;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Cobranzas_Vittoria.Tests.Setup;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

[TestFixture]
public class SeguridadAuthorizationTests
{
    [TestCase("/api/seguridad/usuarios")]
    [TestCase("/api/seguridad/roles")]
    [TestCase("/api/seguridad/permisos")]
    public async Task EndpointsSeguridad_SinToken_RetornanUnauthorized(string endpoint)
    {
        using HttpClient client = GlobalSetupFixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(endpoint);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EndpointsSeguridad_ConTokenInvalidoOExpirado_RetornanUnauthorized(bool expirado)
    {
        using HttpClient client = GlobalSetupFixture.Factory.CreateClient();
        string token = expirado
            // JwtBearer admite cinco minutos de desfase por defecto.
            ? JwtTestTokenFactory.CrearToken(expiraEnUtc: DateTime.UtcNow.AddMinutes(-10))
            : "token-invalido";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.GetAsync("/api/seguridad/usuarios");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
