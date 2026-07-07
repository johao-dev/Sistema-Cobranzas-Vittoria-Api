using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Externos;

/// <summary>
/// Pruebas de TipoCambioController (1 endpoint: GET /api/tipo-cambio?fecha=YYYY-MM-DD).
///
/// El controller delega en ISunatService.ConsultarTipoCambio(fecha).
/// En los tests se inyecta el SunatFake (singleton) que ya tiene un stub para
/// este metodo y devuelve valores fijos: buy_price=3.750, sell_price=3.780,
/// base_currency=USD, quote_currency=PEN.
///
/// El DTO usa [JsonPropertyName] en snake_case:
///   - PrecioCompra       -> buy_price
///   - PrecioVenta        -> sell_price
///   - MonedaBase         -> base_currency
///   - CotizacionDeDivisa -> quote_currency
///   - Fecha              -> date
///
/// Comportamiento del fake:
///   - Si recibe fecha: la usa tal cual en el campo "date"
///   - Si NO recibe fecha: usa DateTime.Today.ToString("yyyy-MM-dd")
/// </summary>
public class TipoCambioControllerTests : IntegrationTestBase
{
    [Test]
    public async Task Get_ConFechaEspecifica_RetornaOkYEstructuraSnakeCase()
    {
        // Arrange
        var fecha = "2026-07-15";

        // Act
        var response = await _client.GetAsync($"/api/tipo-cambio?fecha={fecha}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // El DTO se serializa en snake_case por los [JsonPropertyName]
        Assert.That(JsonHelpers.GetString(body, "buy_price"), Is.Not.Empty);
        Assert.That(JsonHelpers.GetString(body, "sell_price"), Is.Not.Empty);
        Assert.That(JsonHelpers.GetString(body, "base_currency"), Is.Not.Empty);
        Assert.That(JsonHelpers.GetString(body, "quote_currency"), Is.Not.Empty);
        Assert.That(JsonHelpers.GetString(body, "date"), Is.Not.Empty);
    }

    [Test]
    public async Task Get_ConFechaSolicitada_RetornaEsaMismaFechaEnRespuesta()
    {
        // Arrange
        var fecha = "2026-07-15";

        // Act
        var response = await _client.GetAsync($"/api/tipo-cambio?fecha={fecha}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "date"), Is.EqualTo(fecha));
    }

    [Test]
    public async Task Get_SinFecha_RetornaOkConFechaActualPeru()
    {
        // Arrange - el fake usa DateTime.Today.ToString("yyyy-MM-dd") cuando no se envia fecha
        var fechaEsperada = DateTime.Today.ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync("/api/tipo-cambio");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "date"), Is.EqualTo(fechaEsperada));
    }

    [Test]
    public async Task Get_VerificaValoresExactosDelFake()
    {
        // Arrange - el SunatFake devuelve estos valores fijos en ConsultarTipoCambio
        var fecha = "2026-07-15";

        // Act
        var response = await _client.GetAsync($"/api/tipo-cambio?fecha={fecha}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "buy_price"), Is.EqualTo("3.750"));
        Assert.That(JsonHelpers.GetString(body, "sell_price"), Is.EqualTo("3.780"));
        Assert.That(JsonHelpers.GetString(body, "base_currency"), Is.EqualTo("USD"));
        Assert.That(JsonHelpers.GetString(body, "quote_currency"), Is.EqualTo("PEN"));
    }

    [Test]
    public async Task Post_EndpointSoloGet_DevuelveMethodNotAllowed()
    {
        // Arrange - /api/tipo-cambio SOLO tiene [HttpGet]. POST a esa ruta -> 405.

        // Act
        var response = await _client.PostAsync("/api/tipo-cambio", new StringContent(""));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
    }
}
