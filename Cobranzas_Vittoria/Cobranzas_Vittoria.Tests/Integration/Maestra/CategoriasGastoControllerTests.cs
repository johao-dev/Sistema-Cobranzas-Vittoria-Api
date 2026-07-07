using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Maestra;

/// <summary>
/// Pruebas de CategoriasGastoController.
///   GET    /api/contable/categorias-gasto                  -> List
///   POST   /api/contable/categorias-gasto                  -> Upsert (insert si Id=0, update si Id>0)
///   PUT    /api/contable/categorias-gasto                  -> Upsert (con Id en body)
///   DELETE /api/contable/categorias-gasto/{id}             -> Delete (soft: Activo=0)
/// Notas:
///   * El controller NO expone GET /{id}, así que el camino triste 404 no aplica.
///   * Delete hace un UPDATE Activo=0 (soft delete) - no elimina la fila.
///   * Validaciones de nombre vacío/dup las hace el repository inline, no el SP.
/// </summary>
public class CategoriasGastoControllerTests : IntegrationTestBase
{
    [Test]
    public async Task List_RetornaLasCategoriasDelSeed()
    {
        // Act
        var response = await _client.GetAsync("/api/maestra/categorias-gasto");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var items = await response.Content.ReadFromJsonAsync<List<CategoriaGastoUpsertDto>>();
        Assert.That(items, Is.Not.Null);
        // El seed mete 16 categorias activas
        Assert.That(items!.Count, Is.GreaterThanOrEqualTo(1));
        // Verificamos que al menos una está activa
        Assert.That(items!.Any(c => c.Activo), Is.True);
    }

    [Test]
    public async Task List_ConFiltroActivoFalse_ExcluyeActivos()
    {
        // Arrange - creamos una categoria inactiva
        var inactiva = new CategoriaGastoUpsertDto
        {
            Nombre = $"CAT-INACTIVA-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = false
        };
        await _client.PostAsJsonAsync("/api/maestra/categorias-gasto", inactiva);

        // Act
        var response = await _client.GetAsync("/api/maestra/categorias-gasto?activo=false");
        var items = await response.Content.ReadFromJsonAsync<List<CategoriaGastoUpsertDto>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items!.All(c => !c.Activo), Is.True);
        Assert.That(items!.Any(c => c.Nombre == inactiva.Nombre), Is.True);
    }

    [Test]
    public async Task Create_ConNombreValido_RetornaOkYAsignaIdYPersisteEnBD()
    {
        // Arrange
        var dto = new CategoriaGastoUpsertDto
        {
            Nombre = $"CAT-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/categorias-gasto", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var idAsignado = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idCategoriaGasto").GetInt32();
        Assert.That(idAsignado, Is.GreaterThan(0));

        // Assert - 2: BD
        var nombreEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Nombre FROM maestra.CategoriaGasto WHERE IdCategoriaGasto = @id",
            new { id = idAsignado });
        Assert.That(nombreEnBd, Is.EqualTo(dto.Nombre));
    }

    [Test]
    public async Task Create_ConNombreVacio_Retorna5xxPorValidacionDelRepository()
    {
        // Arrange
        // El controller no valida, pero el repository hace
        //   if (string.IsNullOrWhiteSpace(dto.Nombre)) throw new InvalidOperationException
        // que ApiExceptionMiddleware mapea a 500.
        var dto = new CategoriaGastoUpsertDto
        {
            Nombre = "",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/categorias-gasto", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

        // No debe haberse insertado nada (el nombre vacío es inválido)
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.CategoriaGasto WHERE Nombre IS NULL OR LTRIM(RTRIM(Nombre)) = ''");
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaOkYSobreescribeEnBD()
    {
        // Arrange
        var dtoOriginal = new CategoriaGastoUpsertDto
        {
            Nombre = $"CAT-UPD-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/categorias-gasto", dtoOriginal);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idCategoriaGasto").GetInt32();

        var dtoModificado = new CategoriaGastoUpsertDto
        {
            // PUT lleva el id en el body
            IdCategoriaGasto = id,
            Nombre = dtoOriginal.Nombre,
            Activo = false
        };

        // Act
        // El controller tiene [HttpPut("{categoriaId:int}")], la URL debe llevar el id.
        var response = await _client.PutAsJsonAsync($"/api/maestra/categorias-gasto/{id}", dtoModificado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var activoEnBd = await DbHelpers.QueryScalarAsync<bool>(
            "SELECT Activo FROM maestra.CategoriaGasto WHERE IdCategoriaGasto = @id",
            new { id });
        Assert.That(activoEnBd, Is.False);
    }

    [Test]
    public async Task Delete_ConIdExistente_RetornaOkYMarcaInactivoEnBD()
    {
        // Arrange
        var dto = new CategoriaGastoUpsertDto
        {
            Nombre = $"CAT-DEL-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/categorias-gasto", dto);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idCategoriaGasto").GetInt32();

        // Act
        var response = await _client.DeleteAsync($"/api/maestra/categorias-gasto/{id}");

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert - 2: BD
        // El repository hace UPDATE Activo=0 (soft delete).
        // La fila debe seguir existiendo, pero con Activo=0.
        var filas = await DbHelpers.QueryAsync<CategoriaGastoUpsertDto>(
            "SELECT IdCategoriaGasto AS IdCategoriaGasto, Nombre, Activo FROM maestra.CategoriaGasto WHERE IdCategoriaGasto = @id",
            new { id });
        Assert.That(filas, Has.Exactly(1).Items, "La fila no debe borrarse, solo marcar Activo=0.");
        Assert.That(filas.First().Activo, Is.False);
    }
}
