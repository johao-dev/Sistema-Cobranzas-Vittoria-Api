using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Maestra;

/// <summary>
/// Pruebas de UnidadMedidaController.
///   GET  /api/maestra/unidades-medida?activo=
///   POST /api/maestra/unidades-medida              (Upsert)
///   PUT  /api/maestra/unidades-medida/{id}         (Upsert)
/// El SP maestra.usp_UnidadMedida_Upsert:
///   * THROW 50001 si Codigo o Nombre vacíos
///   * THROW 50001 si Codigo duplicado (en otro Id)
///   * THROW 50001 si Nombre duplicado (en otro Id)
///   * Hace UPPER(TRIM(Codigo)) antes de validar
/// </summary>
public class UnidadMedidaControllerTests : IntegrationTestBase
{
    [Test]
    public async Task List_RetornaLasUnidadesDelSeed()
    {
        // Act
        var response = await _client.GetAsync("/api/maestra/unidades-medida");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var items = await response.Content.ReadFromJsonAsync<List<UnidadMedidaDto>>();
        Assert.That(items, Is.Not.Null);
        // El seed incluye estas tres
        Assert.That(items!.Any(u => u.IdUnidadMedida == SeedIds.UnidadMedidaUnd), Is.True);
        Assert.That(items!.Any(u => u.IdUnidadMedida == SeedIds.UnidadMedidaKg), Is.True);
        Assert.That(items!.Any(u => u.IdUnidadMedida == SeedIds.UnidadMedidaBol), Is.True);
    }

    [Test]
    public async Task Create_ConCodigoYNombreValidos_RetornaOkAsignaIdYPersisteEnBD()
    {
        // Arrange
        var dto = new UnidadMedidaUpsertDto
        {
            Codigo = $"UM-{Guid.NewGuid():N}".Substring(0, 10).ToUpper(),
            Nombre = $"Unidad Test {Guid.NewGuid():N}".Substring(0, 30),
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/unidades-medida", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        int idAsignado = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idUnidadMedida").GetInt32();
        Assert.That(idAsignado, Is.GreaterThan(0));

        // Assert - 2: BD
        var nombreEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Nombre FROM maestra.UnidadMedida WHERE IdUnidadMedida = @id",
            new { id = idAsignado });
        Assert.That(nombreEnBd, Is.EqualTo(dto.Nombre));
    }

    [Test]
    public async Task Create_ConCodigoVacio_Retorna5xxPorThrowDelSP()
    {
        // Arrange
        var dto = new UnidadMedidaUpsertDto
        {
            Codigo = "",
            Nombre = "Nombre válido",
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/unidades-medida", dto);

        // Assert
        // El SP lanza THROW 50001 porque @Codigo queda vacío tras TRIM.
        // ApiExceptionMiddleware lo traduce a 500.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

        // No se debe haber insertado nada con nombre "Nombre válido"
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.UnidadMedida WHERE Nombre = @n",
            new { n = "Nombre válido" });
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task Create_ConCodigoDuplicado_Retorna5xxYNoCreaDuplicado()
    {
        // Arrange - tomamos un código del seed (UND)
        var dto = new UnidadMedidaUpsertDto
        {
            Codigo = "UND",  // ya existe en el seed (Id=12)
            Nombre = $"Duplicado-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/unidades-medida", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.UnidadMedida WHERE Codigo = @c",
            new { c = "UND" });
        Assert.That(count, Is.EqualTo(1), "Solo debe existir el UND del seed.");
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaOkYSobreescribeEnBD()
    {
        // Arrange
        var dtoOriginal = new UnidadMedidaUpsertDto
        {
            Codigo = $"UM-{Guid.NewGuid():N}".Substring(0, 10).ToUpper(),
            Nombre = $"Original-{Guid.NewGuid():N}".Substring(0, 20),
            Activo = true
        };
        var createResp = await _client.PostAsJsonAsync("/api/maestra/unidades-medida", dtoOriginal);
        int id = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idUnidadMedida").GetInt32();

        var dtoModificado = new UnidadMedidaUpsertDto
        {
            Codigo = dtoOriginal.Codigo,
            Nombre = "Nombre Modificado",
            Activo = false
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/maestra/unidades-medida/{id}", dtoModificado);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Verificamos el cambio de Nombre y de Activo directamente en BD.
        // Usamos un DTO anónimo plano para evitar problemas con ValueTuples
        // y mapeo por nombre de columna.
        var filas = await DbHelpers.QueryAsync<UnidadMedidaDto>(
            "SELECT IdUnidadMedida, Codigo, Nombre, Activo FROM maestra.UnidadMedida WHERE IdUnidadMedida = @id",
            new { id });
        var fila = filas.First();
        Assert.That(fila.Nombre, Is.EqualTo("Nombre Modificado"));
        Assert.That(fila.Activo, Is.False);
    }
}
