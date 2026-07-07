using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Dtos.Valorizaciones;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration;

/// <summary>
/// Pruebas transversales del ApiExceptionMiddleware.
///
/// El middleware SOLO distingue dos categorias de excepciones:
///   - SqlException           -> 500 + { ok:false, error:"SQL_ERROR",       message }
///   - Cualquier otra Exception -> 500 + { ok:false, error:"UNHANDLED_ERROR", message }
///
/// NO traduce InvalidOperationException de negocio a 400 ni a BadRequest;
/// la trata como "UNHANDLED_ERROR". Esto es deuda tecnica documentada.
///
/// Ademas se valida el comportamiento del pipeline HTTP:
///   - Body vacio o JSON malformado -> 400 (ModelState)
///   - Metodo HTTP no soportado en la ruta -> 405 Method Not Allowed
/// </summary>
public class ApiExceptionMiddlewareTests : IntegrationTestBase
{
    // IDs y constantes para GastosProyecto
    private const int IdProyecto = 10;
    private const int IdProveedorTerreno = 2;
    private const string TipoModuloTerreno = "Terreno";

    [Test]
    public async Task Post_MontoInvalidoGastosProyecto_Devuelve500ConUnhandledError()
    {
        // Arrange - el repositorio valida "soles <= 0 && dolares <= 0" y lanza InvalidOperationException
        // ("Debes ingresar un monto en soles o dolares."). El middleware lo captura como Exception
        // generica -> 500 + UNHANDLED_ERROR (no 400).
        var dto = new GastoProyectoUpsertDto
        {
            IdProyecto = IdProyecto,
            IdProveedorTerreno = IdProveedorTerreno,
            TipoModulo = TipoModuloTerreno,
            Concepto = "TEST CONCEPTO",
            Fecha = DateTime.Today,
            Moneda = "PEN",
            MontoSoles = 0m,
            MontoDolares = 0m,
            Estado = "Activo",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/contable/gastos-proyecto/{TipoModuloTerreno}", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("UNHANDLED_ERROR"));
        Assert.That(JsonHelpers.GetString(body, "message"), Does.Contain("Debes ingresar un monto"));
    }

    [Test]
    public async Task Post_ReglaProveedor_Devuelve500ConSqlError()
    {
        // Arrange - el SP maestra.usp_ProveedorReglaValorizacion_Upsert solo acepta 3 parametros
        // pero el repo le pasa 4 (incluye dto.Usuario). SqlException -> 500 + SQL_ERROR.
        var dto = new ProveedorReglaValorizacionUpsertDto
        {
            IdProveedor = 2,
            PorcentajeGarantia = 0.05m,
            PorcentajeDetraccion = 0.04m,
            Usuario = "test"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/contable/valorizaciones/reglas-proveedor", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("SQL_ERROR"));
    }

    [Test]
    public async Task Post_BodyVacio_DevuelveBadRequest()
    {
        // Arrange - body completamente vacio (sin JSON). ASP.NET invalida el ModelState del DTO
        // antes de llegar al controller y responde 400.
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/login", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Post_JsonMalformado_DevuelveBadRequest()
    {
        // Arrange - JSON incompleto: solo el corchete de apertura, sin cerrar.
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/login", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Get_EndpointSoloPost_DevuelveMethodNotAllowed()
    {
        // Arrange - /api/contable/valorizaciones/reglas-proveedor SOLO tiene [HttpPost].
        // Un GET a esa misma ruta no matchea ninguna accion -> 405.

        // Act
        var response = await _client.GetAsync("/api/contable/valorizaciones/reglas-proveedor");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
    }
}
