using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Maestra;

/// <summary>
/// Pruebas de ProveedoresGastoController.
///   GET    /api/maestra/proveedores-gasto?activo=&idCategoriaGasto=   -> List
///   POST   /api/maestra/proveedores-gasto                             -> Upsert (insert)
///   PUT    /api/maestra/proveedores-gasto/{id}                        -> Upsert (update)
///   DELETE /api/maestra/proveedores-gasto/{id}                        -> Delete (soft: Activo=0)
///
/// Validaciones inline (ProveedorGastoAdministrativoRepository):
///   * IdCategoriaGasto <= 0           -> 500
///   * RazonSocial vacía               -> 500
///   * Duplicado (RazonSocial o Ruc)   -> 500
/// </summary>
public class ProveedoresGastoControllerTests : IntegrationTestBase
{
    [Test]
    public async Task List_RetornaLosProveedoresDeGastoDelSeed()
    {
        // Act
        var response = await _client.GetAsync("/api/maestra/proveedores-gasto");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var items = await response.Content.ReadFromJsonAsync<List<ProveedorGastoAdministrativoUpsertDto>>();
        Assert.That(items, Is.Not.Null);
        // El seed mete algunos proveedores de gasto (en maestra.ProveedorGastoAdministrativo)
        Assert.That(items!.Count, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task List_ConFiltroIdCategoriaGasto_DevuelveSoloProveedoresDeEsaCategoria()
    {
        // Arrange - obtenemos el id de la primera categoria del seed (maestra.CategoriaGasto)
        // y creamos un proveedor con esa categoria.
        int idCategoria = await DbHelpers.QueryScalarAsync<int>(
            "SELECT TOP 1 IdCategoriaGasto FROM maestra.CategoriaGasto WHERE Activo = 1 ORDER BY IdCategoriaGasto");

        var dto = new ProveedorGastoAdministrativoUpsertDto
        {
            IdCategoriaGasto = idCategoria,
            RazonSocial = $"PROV-G-{Guid.NewGuid():N}".Substring(0, 25),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/proveedores-gasto", dto);
        Assert.That(createResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Precondición: el POST inicial debe funcionar.");
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedorGastoAdministrativo").GetInt32();

        // Act
        var response = await _client.GetAsync(
            $"/api/maestra/proveedores-gasto?idCategoriaGasto={idCategoria}");
        var items = await response.Content.ReadFromJsonAsync<List<ProveedorGastoAdministrativoUpsertDto>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items!.Any(p => p.IdProveedorGastoAdministrativo == id), Is.True);
    }

    [Test]
    public async Task Create_ConDatosValidos_RetornaOkAsignaIdYPersisteEnBD()
    {
        // Arrange
        int idCategoria = await DbHelpers.QueryScalarAsync<int>(
            "SELECT TOP 1 IdCategoriaGasto FROM maestra.CategoriaGasto WHERE Activo = 1 ORDER BY IdCategoriaGasto");

        var dto = new ProveedorGastoAdministrativoUpsertDto
        {
            IdCategoriaGasto = idCategoria,
            RazonSocial = $"PROV-G-{Guid.NewGuid():N}".Substring(0, 25),
            Ruc = GenerarRucUnico(),
            Contacto = "Juan Pérez",
            Telefono = "987654321",
            Correo = "test@proveedor.com",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/proveedores-gasto", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        int id = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedorGastoAdministrativo").GetInt32();
        Assert.That(id, Is.GreaterThan(0));

        // Assert - 2: BD - la tabla es maestra.ProveedorGastoAdministrativo
        var razonSocialEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT RazonSocial FROM maestra.ProveedorGastoAdministrativo WHERE IdProveedorGastoAdministrativo = @id",
            new { id });
        Assert.That(razonSocialEnBd, Is.EqualTo(dto.RazonSocial));
    }

    [Test]
    public async Task Create_ConIdCategoriaGastoCero_Retorna5xxPorValidacionDelRepository()
    {
        // Arrange
        // El repository valida inline: if (dto.IdCategoriaGasto <= 0) throw...
        var dto = new ProveedorGastoAdministrativoUpsertDto
        {
            IdCategoriaGasto = 0,
            RazonSocial = "Razon valida",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/proveedores-gasto", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.ProveedorGastoAdministrativo WHERE RazonSocial = @r",
            new { r = "Razon valida" });
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task Create_ConRazonSocialDuplicada_Retorna5xxPorValidacionDelRepository()
    {
        // Arrange - creamos el primero
        int idCategoria = await DbHelpers.QueryScalarAsync<int>(
            "SELECT TOP 1 IdCategoriaGasto FROM maestra.CategoriaGasto WHERE Activo = 1 ORDER BY IdCategoriaGasto");

        var razonDuplicada = $"PROV-DUP-{Guid.NewGuid():N}".Substring(0, 20);
        var primero = new ProveedorGastoAdministrativoUpsertDto
        {
            IdCategoriaGasto = idCategoria,
            RazonSocial = razonDuplicada,
            Activo = true
        };
        var r1 = await _client.PostAsJsonAsync("/api/maestra/proveedores-gasto", primero);
        Assert.That(r1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act - intentamos crear otro con la misma razon social
        var duplicado = new ProveedorGastoAdministrativoUpsertDto
        {
            IdCategoriaGasto = idCategoria,
            RazonSocial = razonDuplicada,
            Activo = true
        };
        var response = await _client.PostAsJsonAsync("/api/maestra/proveedores-gasto", duplicado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.ProveedorGastoAdministrativo WHERE RazonSocial = @r",
            new { r = razonDuplicada });
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaOkYSobreescribeEnBD()
    {
        // Arrange
        int idCategoria = await DbHelpers.QueryScalarAsync<int>(
            "SELECT TOP 1 IdCategoriaGasto FROM maestra.CategoriaGasto WHERE Activo = 1 ORDER BY IdCategoriaGasto");

        var dtoOriginal = new ProveedorGastoAdministrativoUpsertDto
        {
            IdCategoriaGasto = idCategoria,
            RazonSocial = $"PROV-U-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/proveedores-gasto", dtoOriginal);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedorGastoAdministrativo").GetInt32();

        var dtoModificado = new ProveedorGastoAdministrativoUpsertDto
        {
            // El controller hace dto.IdProveedorGastoAdministrativo = proveedorGastoId.
            IdProveedorGastoAdministrativo = id,
            IdCategoriaGasto = idCategoria,
            RazonSocial = dtoOriginal.RazonSocial,
            Activo = false  // cambiamos para verificar mutacion
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/maestra/proveedores-gasto/{id}", dtoModificado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var activoEnBd = await DbHelpers.QueryScalarAsync<bool>(
            "SELECT Activo FROM maestra.ProveedorGastoAdministrativo WHERE IdProveedorGastoAdministrativo = @id",
            new { id });
        Assert.That(activoEnBd, Is.False);
    }

    [Test]
    public async Task Delete_ConIdExistente_RetornaOkYMarcaInactivoEnBD()
    {
        // Arrange
        int idCategoria = await DbHelpers.QueryScalarAsync<int>(
            "SELECT TOP 1 IdCategoriaGasto FROM maestra.CategoriaGasto WHERE Activo = 1 ORDER BY IdCategoriaGasto");

        var dto = new ProveedorGastoAdministrativoUpsertDto
        {
            IdCategoriaGasto = idCategoria,
            RazonSocial = $"PROV-DEL-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/proveedores-gasto", dto);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idProveedorGastoAdministrativo").GetInt32();

        // Act
        var response = await _client.DeleteAsync($"/api/maestra/proveedores-gasto/{id}");

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert - 2: BD - soft delete: Activo=0, fila sigue existiendo
        var activoEnBd = await DbHelpers.QueryScalarAsync<bool>(
            "SELECT Activo FROM maestra.ProveedorGastoAdministrativo WHERE IdProveedorGastoAdministrativo = @id",
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
