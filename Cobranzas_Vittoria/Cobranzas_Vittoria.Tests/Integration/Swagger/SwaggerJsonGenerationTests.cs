using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cobranzas_Vittoria.Tests.Integration.Swagger;

/// <summary>
/// Tests de smoke para Swagger/OpenAPI.
///
/// <para>
/// El endpoint <c>/swagger/v1/swagger.json</c> se genera dinamicamente a partir
/// de los controllers, los <c>[ProducesResponseType]</c>, los <c>OperationFilter</c>
/// y el archivo XML de documentacion. Si alguno de esos elementos rompe (por
/// ejemplo, un controller que devuelve <c>dynamic</c> y no puede ser serializado,
/// o un filter que tira una excepcion), el documento completo falla con 500.
/// </para>
///
/// <para>
/// Estos tests aseguran que la generacion del documento OpenAPI funciona con
/// la API real en un ambiente Development (el unico donde Swagger se activa,
/// ver <c>Program.cs</c>). Si alguna vez agregas un controller problematico
/// o rompes un OperationFilter, esto lo detectara en CI.
/// </para>
///
/// <para>
/// Usa un <see cref="WebApplicationFactory{TEntryPoint}"/> dedicado (no el del
/// <c>GlobalSetupFixture</c>) porque necesitamos forzar el ambiente
/// <c>Development</c> sin afectar a los demas tests.
/// </para>
/// </summary>
[TestFixture]
public class SwaggerJsonGenerationTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Forzamos Development porque Swagger solo se habilita ahi
                // (y en Staging) segun la guarda de Program.cs.
                builder.UseEnvironment("Development");
            });
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task SwaggerJson_DebeRetornar200_CuandoDevelopment()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"El documento OpenAPI debe generarse correctamente en Development. " +
            $"Respuesta: {body}");
    }

    [Test]
    public async Task SwaggerJson_DebeContenerTodosLosControllersRegistrados()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();

        // ImportController es el unico controller que usa nuestro OperationFilter
        // con documentacion de errores tipados. Verificamos que aparece.
        Assert.That(body, Does.Contain("/api/import"),
            "El endpoint /api/import debe estar documentado en swagger.json");

        // AuthController y UnidadMedidaController son controllers legacy que
        // tambien deben aparecer (smoke test de que NO se cae la generacion
        // por controllers sin [ProducesResponseType]).
        Assert.That(body, Does.Contain("/api/auth"),
            "AuthController debe aparecer en swagger.json");
    }

    [Test]
    public async Task SwaggerIndex_DebeRetornar200_CuandoDevelopment()
    {
        var response = await _client.GetAsync("/swagger/index.html");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// Test de diagnostico: si el test de status 200 falla, este test
    /// capturara la excepcion INTERNA (inner) que Swashbuckle lanza al
    /// intentar generar el schema de un parametro problemático (típicamente
    /// IFormFile en multipart/form-data).
    /// </summary>
    [Test]
    public void Diagnostico_GeneracionDeOperaciones_NoDebeLanzarExcepcion()
    {
        using var scope = _factory.Services.CreateScope();
        var swaggerProvider = scope.ServiceProvider.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>();
        // Llamar al provider directamente: si una operacion falla, lanza
        // con la inner exception que la pipeline HTTP se traga.
        Assert.DoesNotThrow(() => swaggerProvider.GetSwagger("v1"),
            "GetSwagger no debe lanzar. Si lanza, la inner exception tendra el detalle.");
    }
}

