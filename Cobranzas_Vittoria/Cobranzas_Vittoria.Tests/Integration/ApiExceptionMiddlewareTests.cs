using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Valorizaciones;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration;

/// <summary>Pruebas transversales de traducción HTTP y del pipeline.</summary>
public class ApiExceptionMiddlewareTests : IntegrationTestBase
{
    [Test]
    public async Task Get_EstadoContableInvalido_Devuelve400Seguro()
    {
        var response = await _client.GetAsync(
            "/api/contable/gastos-directos?estado=estado-inexistente");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("SOLICITUD_INVALIDA"));
        Assert.That(JsonHelpers.GetString(body, "message"), Does.Contain("REGISTRADO"));
    }

    [Test]
    public async Task Post_ReglaProveedor_Devuelve500ConSqlError()
    {
        var dto = new ProveedorReglaValorizacionUpsertDto
        {
            IdProveedor = 2,
            PorcentajeGarantia = 0.05m,
            PorcentajeDetraccion = 0.04m,
            Usuario = "test"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/contable/valorizaciones/reglas-proveedor", dto);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("SQL_ERROR"));
    }

    [Test]
    public async Task Post_BodyVacio_DevuelveBadRequest()
    {
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/seguridad/permisos", content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Post_JsonMalformado_DevuelveBadRequest()
    {
        using var content = new StringContent("{", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/seguridad/permisos", content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Get_EndpointSoloPost_DevuelveMethodNotAllowed()
    {
        var response = await _client.GetAsync(
            "/api/contable/valorizaciones/reglas-proveedor");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
    }
}
