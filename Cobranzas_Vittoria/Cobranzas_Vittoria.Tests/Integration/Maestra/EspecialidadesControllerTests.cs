using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Maestra;

/// <summary>
/// Pruebas de EspecialidadesController.
///   GET  /api/maestra/especialidades?activo=
///   POST /api/maestra/especialidades              (Upsert)
///   PUT  /api/maestra/especialidades/{id}         (Upsert)
/// El SP maestra.usp_Especialidad_Upsert:
///   * THROW 50010 si Nombre es vacío
///   * THROW 50011 si Nombre ya existe (insert)
///   * THROW 50012 si Nombre ya existe en otro Id (update)
/// Por tanto el camino triste se prueba contra el SP, no contra 404 (no hay GET /{id}).
/// </summary>
public class EspecialidadesControllerTests : IntegrationTestBase
{
    [Test]
    public async Task List_RetornaAlMenosLasEspecialidadesDelSeed()
    {
        // Act
        var response = await _client.GetAsync("/api/maestra/especialidades");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var items = await response.Content.ReadFromJsonAsync<List<EspecialidadUpsertDto>>();
        Assert.That(items, Is.Not.Null);
        // El seed mete la Id=2 (ALBAÑILERIA) en Activo=1
        Assert.That(items!.Any(e => e.IdEspecialidad == SeedIds.EspecialidadAlbanileria), Is.True);
    }

    [Test]
    public async Task List_ConFiltroActivoTrue_SoloDevuelveActivos()
    {
        // Arrange - creamos una inactiva
        var inactiva = new EspecialidadUpsertDto
        {
            Nombre = $"ESP-INACTIVA-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = false
        };
        await _client.PostAsJsonAsync("/api/maestra/especialidades", inactiva);

        // Act
        var response = await _client.GetAsync("/api/maestra/especialidades?activo=true");
        var items = await response.Content.ReadFromJsonAsync<List<EspecialidadUpsertDto>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items!.All(e => e.Activo), Is.True);
        Assert.That(items!.Any(e => e.Nombre == inactiva.Nombre), Is.False);
    }

    [Test]
    public async Task Upsert_ConNombreValido_RetornaOkAsignaIdYPersisteEnBD()
    {
        // Arrange
        var dto = new EspecialidadUpsertDto
        {
            Nombre = $"ESP-{Guid.NewGuid():N}".Substring(0, 20),
            Descripcion = "Especialidad creada por test",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/especialidades", dto);

        // Assert - 1: HTTP
        // OJO: ASP.NET Core serializa con camelCase por defecto, por eso "idEspecialidad".
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var idAsignado = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idEspecialidad").GetInt32();
        Assert.That(idAsignado, Is.GreaterThan(0));

        // Assert - 2: BD
        var nombreEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Nombre FROM maestra.Especialidad WHERE IdEspecialidad = @id",
            new { id = idAsignado });
        Assert.That(nombreEnBd, Is.EqualTo(dto.Nombre));
    }

    [Test]
    public async Task Upsert_ConNombreDuplicado_Retorna5xxPorThrowDelSP()
    {
        // Arrange - creamos la primera
        var nombreDuplicado = $"ESP-DUP-{Guid.NewGuid():N}".Substring(0, 20);
        var primero = new EspecialidadUpsertDto { Nombre = nombreDuplicado, Activo = true };
        var r1 = await _client.PostAsJsonAsync("/api/maestra/especialidades", primero);
        Assert.That(r1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act - intentamos crear otra con el mismo nombre
        var duplicado = new EspecialidadUpsertDto { Nombre = nombreDuplicado, Activo = true };
        var response = await _client.PostAsJsonAsync("/api/maestra/especialidades", duplicado);

        // Assert
        // El SP hace THROW 50011, que burbujea como 500 Internal Server Error.
        // El ApiExceptionMiddleware (Program.cs) lo mapea a 500.
        // Verificamos que NO se creó un segundo registro.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Especialidad WHERE Nombre = @n",
            new { n = nombreDuplicado });
        Assert.That(count, Is.EqualTo(1),
            "El SP debería haber abortado la transacción; no debería existir más de un registro con ese nombre.");
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaOkYSobreescribeEnBD()
    {
        // Arrange
        var dtoOriginal = new EspecialidadUpsertDto
        {
            Nombre = $"ESP-UPD-{Guid.NewGuid():N}".Substring(0, 20),
            Descripcion = "Original",
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/especialidades", dtoOriginal);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idEspecialidad").GetInt32();

        var dtoModificado = new EspecialidadUpsertDto
        {
            Nombre = dtoOriginal.Nombre,
            Descripcion = "Modificada",
            Activo = false  // cambiamos el flag para verificar mutación
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/maestra/especialidades/{id}", dtoModificado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var activoEnBd = await DbHelpers.QueryScalarAsync<bool>(
            "SELECT Activo FROM maestra.Especialidad WHERE IdEspecialidad = @id",
            new { id });
        Assert.That(activoEnBd, Is.False,
            "El campo Activo debería haberse actualizado a false.");
    }
}
