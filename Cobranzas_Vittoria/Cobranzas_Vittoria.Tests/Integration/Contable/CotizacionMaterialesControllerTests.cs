using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

/// <summary>
/// Pruebas de CotizacionMaterialesController.
///   GET  /api/contable/cotizacion-materiales/{idProyecto}                 -> GetByProyecto
///   GET  /api/contable/cotizacion-materiales/resumen/{idProyecto}         -> GetResumenByProyecto
///   POST /api/contable/cotizacion-materiales                             -> Upsert (reemplaza cotizaciones del proyecto)
///
/// El seed mete 14 cotizaciones en contable.CotizacionMaterialEspecialidad
/// para el proyecto 10 (Mayta Capac II), todas con Cotizacion=0.00 y Activo=1.
///
/// Detalle importante: el controller hace EnsureTablesAsync al primer GET.
/// La tabla ya existe por el seed, así que es un IF EXISTS que no hace nada.
/// Pero si por algun motivo la borra Respawn, el controller la recrea
/// con un FK a maestra.Proyecto y maestra.Especialidad.
///
/// Upsert hace:
///   1) UPDATE Activo=0 a todas las del IdProyecto
///   2) UPSERT (UPDATE o INSERT) por cada item
///   3) Commit y devuelve { ok=true, totalCotizacionMateriales=suma }
/// </summary>
public class CotizacionMaterialesControllerTests : IntegrationTestBase
{
    [Test]
    public async Task GetByProyecto_ConProyectoConCotizaciones_RetornaItemsYTotal()
    {
        // Act - el seed mete cotizaciones para el proyecto 10
        var response = await _client.GetAsync(
            $"/api/contable/cotizacion-materiales/{SeedIds.ProyectoMaytaCapacII}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("idProyecto").GetInt32(), Is.EqualTo(SeedIds.ProyectoMaytaCapacII));

        var items = body.GetProperty("items");
        Assert.That(items.ValueKind, Is.EqualTo(JsonValueKind.Array));
        // El seed mete 14 cotizaciones activas para el proyecto 10
        Assert.That(items.GetArrayLength(), Is.GreaterThanOrEqualTo(13));

        // Cada item tiene la estructura esperada
        var first = items[0];
        Assert.That(first.GetProperty("idEspecialidad").GetInt32(), Is.GreaterThan(0));
        Assert.That(first.GetProperty("especialidad").GetString(), Is.Not.Empty);
        Assert.That(first.TryGetProperty("cotizacion", out _), Is.True);

        // totalCotizacionMateriales es la suma de las cotizaciones
        var total = body.GetProperty("totalCotizacionMateriales").GetDecimal();
        // El seed las mete con 0.00, pero es >= 0
        Assert.That(total, Is.GreaterThanOrEqualTo(0m));
    }

    [Test]
    public async Task GetByProyecto_ConProyectoSinCotizaciones_RetornaArrayVacioYTotalCero()
    {
        // Arrange - el seed no tiene cotizaciones para el proyecto 9999.
        // Aseguramos el id de un proyecto del seed para crear uno y luego usar uno inexistente.
        // La forma más limpia: usar un id de proyecto que no existe.
        const int idProyectoInexistente = 99999;

        // Act
        var response = await _client.GetAsync(
            $"/api/contable/cotizacion-materiales/{idProyectoInexistente}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("idProyecto").GetInt32(), Is.EqualTo(idProyectoInexistente));
        Assert.That(body.GetProperty("items").GetArrayLength(), Is.EqualTo(0));
        Assert.That(body.GetProperty("totalCotizacionMateriales").GetDecimal(), Is.EqualTo(0m));
    }

    [Test]
    public async Task GetResumen_ConProyectoSinCompras_RetornaItemsConFacturadoCero()
    {
        // Act - el proyecto 10 tiene cotizaciones pero no compras (porque Respawn limpia las compras)
        var response = await _client.GetAsync(
            $"/api/contable/cotizacion-materiales/resumen/{SeedIds.ProyectoMaytaCapacII}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("idProyecto").GetInt32(), Is.EqualTo(SeedIds.ProyectoMaytaCapacII));

        // Sin compras cargadas, los totales de facturado y saldo son 0
        Assert.That(body.GetProperty("totalFacturado").GetDecimal(), Is.EqualTo(0m));
        // totalCotizacionMateriales y totalSaldo coinciden (sin compras, saldo = cotizacion)
        var totalCot = body.GetProperty("totalCotizacionMateriales").GetDecimal();
        var totalSaldo = body.GetProperty("totalSaldo").GetDecimal();
        Assert.That(totalCot, Is.EqualTo(totalSaldo));

        // Cada item: facturado=0, saldo=cotizacion
        var items = body.GetProperty("items");
        foreach (var item in items.EnumerateArray())
        {
            var cot = item.GetProperty("cotizacion").GetDecimal();
            var fact = item.GetProperty("facturado").GetDecimal();
            var saldo = item.GetProperty("saldo").GetDecimal();
            Assert.That(fact, Is.EqualTo(0m), "Sin compras, facturado debe ser 0.");
            Assert.That(saldo, Is.EqualTo(cot), "saldo debe igualar cotizacion cuando no hay facturado.");
        }
    }

    [Test]
    public async Task Upsert_ConIdProyectoInvalido_RetornaBadRequest()
    {
        // Arrange - el controller valida inline: if (dto.IdProyecto <= 0) return BadRequest
        var dto = new CotizacionMaterialesUpsertDto
        {
            IdProyecto = 0,
            Items = new List<CotizacionMaterialEspecialidadItemDto>
            {
                new() { IdEspecialidad = SeedIds.EspecialidadAlbanileria, Cotizacion = 100m }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contable/cotizacion-materiales", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Upsert_ConItemsValidos_RetornaOkYPersisteEnBD()
    {
        // Arrange - usamos un proyecto del seed y cambiamos la cotizacion de una especialidad
        var dto = new CotizacionMaterialesUpsertDto
        {
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Items = new List<CotizacionMaterialEspecialidadItemDto>
            {
                new() { IdEspecialidad = SeedIds.EspecialidadAlbanileria, Cotizacion = 1234.56m }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contable/cotizacion-materiales", dto);

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("ok").GetBoolean(), Is.True);
        Assert.That(body.GetProperty("totalCotizacionMateriales").GetDecimal(), Is.EqualTo(1234.56m));

        // Assert - 2: BD - verificamos la cotizacion persistida
        var cotEnBd = await DbHelpers.QueryScalarAsync<decimal>(
            "SELECT Cotizacion FROM contable.CotizacionMaterialEspecialidad " +
            "WHERE IdProyecto = @p AND IdEspecialidad = @e AND Activo = 1",
            new { p = SeedIds.ProyectoMaytaCapacII, e = SeedIds.EspecialidadAlbanileria });
        Assert.That(cotEnBd, Is.EqualTo(1234.56m));
    }

    [Test]
    public async Task Upsert_ConItemsDuplicadosMismaEspecialidad_SumaLasCotizaciones()
    {
        // Arrange - el controller agrupa por IdEspecialidad y suma las cotizaciones.
        // Si paso dos items con la misma especialidad, ambos se suman en el mismo UPDATE/INSERT.
        var dto = new CotizacionMaterialesUpsertDto
        {
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Items = new List<CotizacionMaterialEspecialidadItemDto>
            {
                new() { IdEspecialidad = SeedIds.EspecialidadEstructura, Cotizacion = 100m },
                new() { IdEspecialidad = SeedIds.EspecialidadEstructura, Cotizacion = 50m }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contable/cotizacion-materiales", dto);

        // Assert - 1: HTTP - el total debe ser 150 (suma)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("totalCotizacionMateriales").GetDecimal(), Is.EqualTo(150m));

        // Assert - 2: BD - solo debe existir una fila con la cotizacion sumada
        var filas = await DbHelpers.QueryAsync<(decimal Cotizacion, bool Activo)>(
            "SELECT Cotizacion, Activo FROM contable.CotizacionMaterialEspecialidad " +
            "WHERE IdProyecto = @p AND IdEspecialidad = @e",
            new { p = SeedIds.ProyectoMaytaCapacII, e = SeedIds.EspecialidadEstructura });
        var activas = filas.Where(f => f.Activo).ToList();
        Assert.That(activas, Has.Exactly(1).Items, "Solo debe haber una fila activa para esa especialidad.");
        Assert.That(activas[0].Cotizacion, Is.EqualTo(150m));
    }

    [Test]
    public async Task Upsert_LlamaDosVeces_ReemplazaElValorAnteriorSinSumar()
    {
        // Arrange - el UNIQUE INDEX (IdProyecto, IdEspecialidad) garantiza una sola fila por par.
        // El segundo upsert SOBREESCRIBE la fila creada por el primero (no suma, no crea historial).
        // El primer valor (200) se pierde: la fila pasa a valer 75, Activo=1.
        var dto1 = new CotizacionMaterialesUpsertDto
        {
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Items = new List<CotizacionMaterialEspecialidadItemDto>
            {
                new() { IdEspecialidad = SeedIds.EspecialidadCasco, Cotizacion = 200m }
            }
        };
        var dto2 = new CotizacionMaterialesUpsertDto
        {
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            Items = new List<CotizacionMaterialEspecialidadItemDto>
            {
                new() { IdEspecialidad = SeedIds.EspecialidadCasco, Cotizacion = 75m }
            }
        };

        // Act
        await _client.PostAsJsonAsync("/api/contable/cotizacion-materiales", dto1);
        var response2 = await _client.PostAsJsonAsync("/api/contable/cotizacion-materiales", dto2);

        // Assert - 1: el response del segundo upsert reporta el total nuevo (75), no 275
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body2 = await response2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body2.GetProperty("totalCotizacionMateriales").GetDecimal(), Is.EqualTo(75m),
            "El total del segundo upsert debe ser 75, no la suma 275.");

        // Assert - 2: la fila activa tiene el valor del segundo upsert (75), no 200 ni 275
        var cotActiva = await DbHelpers.QueryScalarAsync<decimal>(
            "SELECT Cotizacion FROM contable.CotizacionMaterialEspecialidad " +
            "WHERE IdProyecto = @p AND IdEspecialidad = @e AND Activo = 1",
            new { p = SeedIds.ProyectoMaytaCapacII, e = SeedIds.EspecialidadCasco });
        Assert.That(cotActiva, Is.EqualTo(75m), "El segundo upsert debe reemplazar, no sumar.");

        // Assert - 3: solo existe UNA fila para ese par (gracias al UNIQUE INDEX)
        var totalFilas = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM contable.CotizacionMaterialEspecialidad " +
            "WHERE IdProyecto = @p AND IdEspecialidad = @e",
            new { p = SeedIds.ProyectoMaytaCapacII, e = SeedIds.EspecialidadCasco });
        Assert.That(totalFilas, Is.EqualTo(1),
            "El UNIQUE INDEX garantiza una sola fila por (IdProyecto, IdEspecialidad).");
    }
}
