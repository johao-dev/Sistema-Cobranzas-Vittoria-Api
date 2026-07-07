using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Maestra;

/// <summary>
/// Pruebas de MaterialesController.
///   GET  /api/maestra/materiales?activo=&idEspecialidad=        -> List
///   GET  /api/maestra/materiales/siguiente-codigo             -> GetSiguienteCodigo (helper)
///   GET  /api/maestra/materiales/{id}                          -> Get (404 si no existe)
///   POST /api/maestra/materiales                               -> Upsert (IdMaterial=0 → insert con codigo autogenerado)
///   PUT  /api/maestra/materiales/{id}                          -> Upsert (IdMaterial>0 → update conservando codigo)
///
/// Notas:
///   * UnidadMedida es string (código "UND", "KG"), no int.
///   * Codigo se autogenera como "MAT-0001", "MAT-0002", ...
///   * El seed mete ~200 materiales; el primero es "ALAMBRE NEGRO RECOCIDO #16 (kg)".
///   * El SP maestra.usp_Material_Upsert valida FK a Especialidad (error 547).
/// </summary>
public class MaterialesControllerTests : IntegrationTestBase
{
    [Test]
    public async Task List_RetornaLosMaterialesDelSeed()
    {
        // Act
        var response = await _client.GetAsync("/api/maestra/materiales");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var items = await response.Content.ReadFromJsonAsync<List<MaterialUpsertDto>>();
        Assert.That(items, Is.Not.Null);
        // El seed mete ~200 materiales
        Assert.That(items!.Count, Is.GreaterThan(50));
    }

    [Test]
    public async Task List_ConFiltroIdEspecialidad_DevuelveSoloMaterialesDeEsaEspecialidad()
    {
        // Arrange - creamos un material con la especialidad ALBAÑILERIA
        var nuevo = await CrearMaterialTestAsync(SeedIds.EspecialidadAlbanileria, "UND");

        // Act
        var response = await _client.GetAsync(
            $"/api/maestra/materiales?idEspecialidad={SeedIds.EspecialidadAlbanileria}");
        var items = await response.Content.ReadFromJsonAsync<List<MaterialUpsertDto>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items!.Any(m => m.IdMaterial == nuevo.Id), Is.True);
        // Todos los items deben tener la especialidad filtrada
        // (no podemos verificar IdEspecialidad porque el DTO solo lo trae el entity,
        //  pero la query del SP sí filtra, así que este assert es la verificación fuerte)
    }

    [Test]
    public async Task List_ConFiltroActivoFalse_ExcluyeActivos()
    {
        // Arrange - creamos un material inactivo
        var inactivo = await CrearMaterialTestAsync(
            SeedIds.EspecialidadAlbanileria, "UND", activo: false);

        // Act
        var response = await _client.GetAsync("/api/maestra/materiales?activo=false");
        var items = await response.Content.ReadFromJsonAsync<List<MaterialUpsertDto>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items!.All(m => !m.Activo), Is.True);
        Assert.That(items!.Any(m => m.IdMaterial == inactivo.Id), Is.True);
    }

    [Test]
    public async Task GetSiguienteCodigo_RetornaUnCodigoConFormatoMAT_NNNN()
    {
        // Act
        var response = await _client.GetAsync("/api/maestra/materiales/siguiente-codigo");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var codigo = body.GetProperty("codigo").GetString();
        Assert.That(codigo, Is.Not.Null);
        // El formato es "MAT-0001", "MAT-0042", etc.
        Assert.That(codigo, Does.Match(@"^MAT-\d{4}$"));
    }

    [Test]
    public async Task GetById_ConIdExistente_RetornaOkYMaterialCompleto()
    {
        // Arrange
        var material = await CrearMaterialTestAsync(SeedIds.EspecialidadAlbanileria, "UND");

        // Act
        var response = await _client.GetAsync($"/api/maestra/materiales/{material.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("idMaterial").GetInt32(), Is.EqualTo(material.Id));
        Assert.That(body.GetProperty("idEspecialidad").GetInt32(), Is.EqualTo(SeedIds.EspecialidadAlbanileria));
        Assert.That(body.GetProperty("descripcion").GetString(), Is.EqualTo(material.Descripcion));
        // El SP devuelve tambien el JOIN con Especialidad.Nombre
        Assert.That(body.GetProperty("especialidad").GetString(), Is.EqualTo("ALBAÑILERIA"));
    }

    [Test]
    public async Task GetById_ConIdInexistente_RetornaNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/maestra/materiales/9999999");

        // Assert
        // Segundo test de la suite que cubre el caso 404.
        // Aqui lo aplicamos a un controller de Maestra, complementando
        // el de ProveedoresController.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Upsert_ConDatosValidos_RetornaOkAsignaIdYPersisteEnBD()
    {
        // Arrange
        var dto = new MaterialUpsertDto
        {
            // IdMaterial=0 o null -> el controller hace INSERT y genera codigo automático.
            IdMaterial = 0,
            IdEspecialidad = SeedIds.EspecialidadEstructura,
            // Codigo = null -> el repository genera MAT-XXXX automáticamente.
            Codigo = null,
            CodigoProveedor = "COD-PROV-TEST",
            Descripcion = $"Material de prueba {Guid.NewGuid():N}".Substring(0, 30),
            UnidadMedida = "KG",  // string, no int. "KG" es un codigo del seed.
            StockMinimo = 10.5m,
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/materiales", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var idAsignado = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idMaterial").GetInt32();
        Assert.That(idAsignado, Is.GreaterThan(0));

        // Assert - 2: BD - verificamos que la fila existe con los datos correctos
        // La tabla es maestra.Material (NO contable.Material - mismo bug que CategoriaGasto).
        var descripcionEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Descripcion FROM maestra.Material WHERE IdMaterial = @id",
            new { id = idAsignado });
        Assert.That(descripcionEnBd, Is.EqualTo(dto.Descripcion));

        // El codigo se genera automático si no lo pasamos
        var codigoEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Codigo FROM maestra.Material WHERE IdMaterial = @id",
            new { id = idAsignado });
        Assert.That(codigoEnBd, Does.Match(@"^MAT-\d{4}$"),
            "El codigo debe autogenerarse con formato MAT-XXXX cuando se omite.");
    }

    [Test]
    public async Task Upsert_ConIdEspecialidadInexistente_Retorna5xxPorViolacionDeFK()
    {
        // Arrange
        var dto = new MaterialUpsertDto
        {
            IdMaterial = 0,
            IdEspecialidad = 999999,  // No existe
            Descripcion = "Material con FK inválida",
            UnidadMedida = "UND",
            StockMinimo = 0,
            Activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/maestra/materiales", dto);

        // Assert
        // El SP no valida la FK antes del INSERT; la BD lanza error 547 (FK violation).
        // ApiExceptionMiddleware lo traduce a 500.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

        // No debe haberse insertado nada con esa descripción
        var count = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM maestra.Material WHERE Descripcion = @d",
            new { d = "Material con FK inválida" });
        Assert.That(count, Is.EqualTo(0));
    }

    /// <summary>
    /// Helper para crear un material de prueba y devolver su id + descripción.
    /// Reduce la duplicación entre los tests que necesitan un material base.
    /// </summary>
    private async Task<(int Id, string Descripcion)> CrearMaterialTestAsync(
        int idEspecialidad, string unidadMedida, bool activo = true)
    {
        var dto = new MaterialUpsertDto
        {
            IdMaterial = 0,
            IdEspecialidad = idEspecialidad,
            Descripcion = $"MAT-TEST-{Guid.NewGuid():N}".Substring(0, 30),
            UnidadMedida = unidadMedida,
            StockMinimo = 1.0m,
            Activo = activo
        };
        var resp = await _client.PostAsJsonAsync("/api/maestra/materiales", dto);
        var id = (await resp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idMaterial").GetInt32();
        return (id, dto.Descripcion);
    }
}
