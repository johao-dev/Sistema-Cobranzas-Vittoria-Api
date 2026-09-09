using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

[TestFixture]
public sealed class RequerimientosAuthorizationTests : IntegrationTestBase
{
    [Test]
    public async Task List_SinToken_Retorna401()
    {
        using HttpClient client = GlobalSetupFixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/compras/requerimientos");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task List_TokenSinPermiso_Retorna403()
    {
        using HttpClient client = CrearClienteConPermisos(Array.Empty<string>());

        HttpResponseMessage response = await client.GetAsync("/api/compras/requerimientos");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task List_TokenConPermisoVer_Retorna200()
    {
        using HttpClient client = CrearClienteConPermisos([Permisos.Requerimientos.Ver]);

        HttpResponseMessage response = await client.GetAsync("/api/compras/requerimientos");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Crear_TokenSoloConPermisoVer_Retorna403()
    {
        using HttpClient client = CrearClienteConPermisos([Permisos.Requerimientos.Ver]);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/compras/requerimientos", RequerimientoBuilder.Nuevo().Build());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    private static HttpClient CrearClienteConPermisos(IEnumerable<string> permisos)
    {
        HttpClient client = GlobalSetupFixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTestTokenFactory.CrearToken(permisos: permisos));
        return client;
    }
}
