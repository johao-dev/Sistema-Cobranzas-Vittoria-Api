using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Maestra;

/// <summary>
/// Pruebas de ProveedoresController.
///   GET  /api/maestra/proveedores?activo=&idEspecialidad=
///   GET  /api/maestra/proveedores/{id}                       -> GetById
///   POST /api/maestra/proveedores/{id}/especialidades        -> SetEspecialidad
///   POST /api/maestra/proveedores                            -> Upsert
///   PUT  /api/maestra/proveedores                            -> Upsert
///   GET  /api/maestra/proveedores/consulta-ruc/{ruc}        -> ConsultarRuc (ISunatService)
///
/// El controller valida inline:
///   * Nombre y RUC no vacíos -> 400 BadRequest
/// El SP maestra.usp_Proveedor_Upsert valida:
///   * THROW 50020 si RUC duplicado
///   * THROW 50021/50022 otros casos
/// </summary>
public class ProveedoresControllerTests : IntegrationTestBase
{
    private const string SunatRucExistente = "20123456789";
    private const string SunatRucInexistente = "20999999999";

    [Test]
    public async Task List_RetornaLosProveedoresDelSeed()
    {
        // Act
        var response = await _client.GetAsync("/api/maestra/proveedores");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var items = await response.Content.ReadFromJsonAsync<List<ProveedorUpsertDto>>();
        Assert.That(items, Is.Not.Null);
        // El seed mete 45 proveedores
        Assert.That(items!.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task List_ConFiltroIdEspecialidad_DevuelveSoloProveedoresConEsaEspecialidad()
    {
        // Arrange - creamos un proveedor y le asociamos la especialidad ALBAÑILERIA
        var ruc = GenerarRucUnico();
        var dto = new ProveedorUpsertDto
        {
            Ruc = ruc,
            RazonSocial = $"PROV-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/proveedores", dto);
        int idProveedor = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedor").GetInt32();

        // El endpoint espera ProveedorEspecialidadDto (IdProveedor, IdEspecialidad, Activo),
        // no un int pelado. Por eso el body correcto es:
        var setEsp = await _client.PostAsJsonAsync(
            $"/api/maestra/proveedores/{idProveedor}/especialidades",
            new ProveedorEspecialidadDto
            {
                IdProveedor = idProveedor,
                IdEspecialidad = SeedIds.EspecialidadAlbanileria,
                Activo = true
            });
        Assert.That(setEsp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Falló la asociación de especialidad");

        // Act
        var response = await _client.GetAsync(
            $"/api/maestra/proveedores?idEspecialidad={SeedIds.EspecialidadAlbanileria}");
        var items = await response.Content.ReadFromJsonAsync<List<ProveedorUpsertDto>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items!.Any(p => p.IdProveedor == idProveedor), Is.True);
    }

    [Test]
    public async Task GetById_ConIdExistente_RetornaOkYProveedorCompleto()
    {
        // Arrange
        var dto = new ProveedorUpsertDto
        {
            Ruc = GenerarRucUnico(),
            RazonSocial = $"PROV-GET-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/proveedores", dto);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedor").GetInt32();

        // Act
        var response = await _client.GetAsync($"/api/maestra/proveedores/{id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // El service devuelve new { proveedor, especialidades }.
        // La estructura JSON es { "proveedor": { ... }, "especialidades": [ ... ] }.
        Assert.That(body.TryGetProperty("proveedor", out var prov), Is.True);
        Assert.That(prov.GetProperty("idProveedor").GetInt32(), Is.EqualTo(id));
        Assert.That(prov.GetProperty("razonSocial").GetString(), Is.EqualTo(dto.RazonSocial));
        Assert.That(body.TryGetProperty("especialidades", out var esp), Is.True);
    }

    [Test]
    public async Task GetById_ConIdInexistente_RetornaNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/maestra/proveedores/9999999");

        // Assert
        // Este es el único controller del área Maestra con GET /{id}, así que
        // este test cubre el caso "404 NotFound" que la estrategia exige.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Upsert_ConRucValido_RetornaOkYPersisteEnBD()
    {
        // Arrange
        var dto = new ProveedorUpsertDto
        {
            Ruc = GenerarRucUnico(),
            RazonSocial = $"PROV-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/proveedores", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        int id = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedor").GetInt32();
        Assert.That(id, Is.GreaterThan(0));

        // Assert - 2: BD
        var razonSocialEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT RazonSocial FROM maestra.Proveedor WHERE IdProveedor = @id",
            new { id });
        Assert.That(razonSocialEnBd, Is.EqualTo(dto.RazonSocial));
    }

    [Test]
    public async Task Upsert_ConRucDuplicado_Retorna5xxPorThrowDelSP()
    {
        // Arrange - creamos el primero
        var rucDuplicado = GenerarRucUnico();
        var primero = new ProveedorUpsertDto
        {
            Ruc = rucDuplicado,
            RazonSocial = $"PROV-1-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var r1 = await _client.PostAsJsonAsync("/api/maestra/proveedores", primero);
        Assert.That(r1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act - intentamos crear otro con el mismo RUC
        var duplicado = new ProveedorUpsertDto
        {
            Ruc = rucDuplicado,
            RazonSocial = $"PROV-2-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var response = await _client.PostAsJsonAsync("/api/maestra/proveedores", duplicado);

        // Assert
        // El SP hace THROW 50020, ApiExceptionMiddleware lo traduce a 500.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Proveedor WHERE Ruc = @r",
            new { r = rucDuplicado });
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task ConsultarRuc_CuandoNoExisteEnSunat_RetornaNotFound()
    {
        // Arrange - el SunatFake tiene RucsExistentes vacío por defecto
        // (cada test no configura nada, devuelve null → controller responde 404)

        // Act
        var response = await _client.GetAsync($"/api/maestra/proveedores/consulta-ruc/{SunatRucInexistente}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ConsultarRuc_CuandoExisteEnSunat_RetornaOkConDatos()
    {
        // Arrange - configuramos el mock para que reconozca este RUC
        GlobalSetupFixture.Factory.Sunat.RucsExistentes.Add(SunatRucExistente);

        // Act
        var response = await _client.GetAsync($"/api/maestra/proveedores/consulta-ruc/{SunatRucExistente}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // El DTO usa [JsonPropertyName] con snake_case: razon_social, numero_documento.
        Assert.That(body.GetProperty("numero_documento").GetString(), Is.EqualTo(SunatRucExistente));
        Assert.That(body.GetProperty("razon_social").GetString(), Does.Contain(SunatRucExistente));
    }

    [Test]
    public async Task SetEspecialidad_ConIdValido_AsociaEspecialidadYPersisteEnBD()
    {
        // Arrange
        var dto = new ProveedorUpsertDto
        {
            Ruc = GenerarRucUnico(),
            RazonSocial = $"PROV-ESP-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/proveedores", dto);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedor").GetInt32();

        // Act
        // El endpoint espera ProveedorEspecialidadDto, no un int pelado.
        var response = await _client.PostAsJsonAsync(
            $"/api/maestra/proveedores/{id}/especialidades",
            new ProveedorEspecialidadDto
            {
                IdProveedor = id,
                IdEspecialidad = SeedIds.EspecialidadEstructura,
                Activo = true
            });

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert - 2: BD - verificamos que la asociacion persiste
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.ProveedorEspecialidad WHERE IdProveedor = @p AND IdEspecialidad = @e",
            new { p = id, e = SeedIds.EspecialidadEstructura });
        Assert.That(count, Is.EqualTo(1));
    }

    /// <summary>
    /// Genera un RUC único de 11 dígitos para evitar colisión con el seed.
    /// </summary>
    private static string GenerarRucUnico()
    {
        // "20" + 9 dígitos aleatorios = 11 dígitos
        var random = new Random();
        var suffix = random.NextInt64(0, 1_000_000_000).ToString("D9");
        return "20" + suffix;
    }
}
