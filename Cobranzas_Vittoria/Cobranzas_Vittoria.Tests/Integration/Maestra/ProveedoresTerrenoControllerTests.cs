using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Maestra;

/// <summary>
/// Pruebas de ProveedoresTerrenoController.
///   GET    /api/maestra/proveedores-terreno?activo=   -> List
///   POST   /api/maestra/proveedores-terreno          -> Upsert (insert)
///   PUT    /api/maestra/proveedores-terreno/{id}     -> Upsert (update)
///   DELETE /api/maestra/proveedores-terreno/{id}     -> Delete (soft)
///
/// El seed mete un proveedor de terreno con RazonSocial = "VARGAS" y Ruc = "00000000".
///
/// Validaciones inline (ProveedorTerrenoRepository):
///   * RazonSocial vacía              -> 500
///   * Duplicado (RazonSocial o Ruc)  -> 500
/// </summary>
public class ProveedoresTerrenoControllerTests : IntegrationTestBase
{
    [Test]
    public async Task List_RetornaAlMenosElProveedorVargasDelSeed()
    {
        // Act
        var response = await _client.GetAsync("/api/maestra/proveedores-terreno");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var items = await response.Content.ReadFromJsonAsync<List<ProveedorTerrenoUpsertDto>>();
        Assert.That(items, Is.Not.Null);
        // El seed mete VARGAS como proveedor de terreno
        Assert.That(items!.Any(p => p.RazonSocial == "VARGAS"), Is.True);
    }

    [Test]
    public async Task List_ConFiltroActivoFalse_ExcluyeActivos()
    {
        // Arrange - creamos un proveedor inactivo
        var inactivo = new ProveedorTerrenoUpsertDto
        {
            RazonSocial = $"PROV-T-INACTIVO-{Guid.NewGuid():N}".Substring(0, 25),
            Activo = false
        };
        await _client.PostAsJsonAsync("/api/maestra/proveedores-terreno", inactivo);

        // Act
        var response = await _client.GetAsync("/api/maestra/proveedores-terreno?activo=false");
        var items = await response.Content.ReadFromJsonAsync<List<ProveedorTerrenoUpsertDto>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items!.All(p => !p.Activo), Is.True);
        Assert.That(items!.Any(p => p.RazonSocial == inactivo.RazonSocial), Is.True);
        // VARGAS del seed está Activo=1, no debe aparecer
        Assert.That(items!.All(p => p.RazonSocial != "VARGAS"), Is.True);
    }

    [Test]
    public async Task Create_ConDatosValidos_RetornaOkAsignaIdYPersisteEnBD()
    {
        // Arrange
        var dto = new ProveedorTerrenoUpsertDto
        {
            RazonSocial = $"PROV-T-{Guid.NewGuid():N}".Substring(0, 25),
            Ruc = GenerarRucUnico(),
            Contacto = "Maria Lopez",
            Telefono = "999888777",
            Correo = "maria@terreno.com",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/proveedores-terreno", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        int id = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedorTerreno").GetInt32();
        Assert.That(id, Is.GreaterThan(0));

        // Assert - 2: BD - la tabla es maestra.ProveedorTerreno
        var razonSocialEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT RazonSocial FROM maestra.ProveedorTerreno WHERE IdProveedorTerreno = @id",
            new { id });
        Assert.That(razonSocialEnBd, Is.EqualTo(dto.RazonSocial));
    }

    [Test]
    public async Task Create_ConRazonSocialVacia_Retorna5xxPorValidacionDelRepository()
    {
        // Arrange
        var dto = new ProveedorTerrenoUpsertDto
        {
            RazonSocial = "",
            Ruc = "12345678901",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/proveedores-terreno", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.ProveedorTerreno WHERE Ruc = @r",
            new { r = "12345678901" });
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task Create_ConRazonSocialDuplicada_Retorna5xxPorValidacionDelRepository()
    {
        // Arrange - creamos el primero
        var razonDuplicada = $"PROV-T-DUP-{Guid.NewGuid():N}".Substring(0, 20);
        var primero = new ProveedorTerrenoUpsertDto
        {
            RazonSocial = razonDuplicada,
            Activo = true
        };
        var r1 = await _client.PostAsJsonAsync("/api/maestra/proveedores-terreno", primero);
        Assert.That(r1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act - intentamos crear otro con la misma razon social
        var duplicado = new ProveedorTerrenoUpsertDto
        {
            RazonSocial = razonDuplicada,
            Activo = true
        };
        var response = await _client.PostAsJsonAsync("/api/maestra/proveedores-terreno", duplicado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.ProveedorTerreno WHERE RazonSocial = @r",
            new { r = razonDuplicada });
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaOkYSobreescribeEnBD()
    {
        // Arrange
        var dtoOriginal = new ProveedorTerrenoUpsertDto
        {
            RazonSocial = $"PROV-T-UPD-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/proveedores-terreno", dtoOriginal);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedorTerreno").GetInt32();

        var dtoModificado = new ProveedorTerrenoUpsertDto
        {
            IdProveedorTerreno = id,
            RazonSocial = dtoOriginal.RazonSocial,
            Telefono = "111222333",
            Activo = false
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/maestra/proveedores-terreno/{id}", dtoModificado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var telefonoEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Telefono FROM maestra.ProveedorTerreno WHERE IdProveedorTerreno = @id",
            new { id });
        Assert.That(telefonoEnBd, Is.EqualTo("111222333"));
    }

    [Test]
    public async Task Delete_ConIdExistente_RetornaOkYMarcaInactivoEnBD()
    {
        // Arrange
        var dto = new ProveedorTerrenoUpsertDto
        {
            RazonSocial = $"PROV-T-DEL-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/proveedores-terreno", dto);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedorTerreno").GetInt32();

        // Act
        var response = await _client.DeleteAsync($"/api/maestra/proveedores-terreno/{id}");

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert - 2: BD
        var activoEnBd = await DbHelpers.QueryScalarAsync<bool>(
            "SELECT Activo FROM maestra.ProveedorTerreno WHERE IdProveedorTerreno = @id",
            new { id });
        Assert.That(activoEnBd, Is.False, "Soft delete: la fila debe seguir existiendo con Activo=0.");
    }

    private static string GenerarRucUnico()
    {
        var random = new Random();
        var suffix = random.NextInt64(0, 1_000_000_000).ToString("D9");
        return "20" + suffix;
    }
}
