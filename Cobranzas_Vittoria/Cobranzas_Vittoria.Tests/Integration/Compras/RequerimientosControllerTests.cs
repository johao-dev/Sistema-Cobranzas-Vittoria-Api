using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

/// <summary>
/// Pruebas de RequerimientosController.
///
///   GET    /api/compras/requerimientos?estado=&idEspecialidad=&idProyecto=   -> List
///   GET    /api/compras/requerimientos/{id}                                  -> Get
///   POST   /api/compras/requerimientos                                       -> Crear (TVP)
///   PUT    /api/compras/requerimientos/{id}                                  -> Update
///   PATCH  /api/compras/requerimientos/{id}/estado                           -> UpdateEstado
///   POST   /api/compras/requerimientos/{id}/validacion-almacen               -> ValidarAlmacen
///
/// Estados válidos (CHECK): Registrado, EnviadoOC, GeneradoOC, ValidadoAlmacen, Anulado.
/// Resultados válidos (CHECK): Conforme, Observado.
///
/// Reglas de negocio:
///   - Crear: NumeroRequerimiento es requerido. Si el enviado ya existe, el repo
///     calcula el siguiente como MAX(TRY_CONVERT(INT, NumeroRequerimiento))+1.
///   - Update: el SP reemplaza el set completo de items (DELETE + INSERT). El repo
///     lanza InvalidOperationException si el requerimiento ya tiene OC o estado != 'REGISTRADO'.
///   - Get devuelve { requerimiento, items, validaciones, puedeEditar }.
/// </summary>
public class RequerimientosControllerTests : IntegrationTestBase
{
    // Materiales del seed (IDENTITY arranca en 1; el seed mete ~21 materiales).
    // Material 2 -> IdEspecialidad 2 (Albañilería, Activo=1).
    // Material 6 -> IdEspecialidad 4 (Casco, Activo=1).
    private const int IdMaterialAlbanileria = 2;
    private const int IdMaterialCasco = 6;

    private const int IdUsuarioAlmacen = SeedIds.AlmacenId;

    [Test]
    public async Task List_SinFiltros_RetornaArrayVacio()
    {
        // Act - la tabla compras.Requerimiento se limpia antes de cada test
        // (no está en TablesToIgnore del Respawn).
        var response = await _client.GetAsync("/api/compras/requerimientos");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task List_FiltradoPorEstadoRegistrado_RetornaArrayVacioSiNoHayCoincidencias()
    {
        // Act - sin requerimientos, ningún estado retorna datos
        var response = await _client.GetAsync("/api/compras/requerimientos?estado=Registrado");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task List_FiltradoPorIdProyecto_RetornaRequerimientosCoincidentes()
    {
        // Arrange - crear 2 requerimientos del mismo proyecto
        await RequerimientoBuilder.Nuevo().CrearAsync(_client);
        await RequerimientoBuilder.Nuevo().CrearAsync(_client);

        // Act
        var response = await _client.GetAsync(
            $"/api/compras/requerimientos?idProyecto={SeedIds.ProyectoMaytaCapacII}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task List_FiltradoPorIdEspecialidad_RetornaRequerimientosConEsaEspecialidadEnSusMateriales()
    {
        // Arrange - el filtro por idEspecialidad mira la especialidad de los MATERIALES
        // (no la del requerimiento). Material 2 -> Especialidad 2 (Albañilería).
        await RequerimientoBuilder.Nuevo()
            .ConItem(IdMaterialAlbanileria, 5m, "Mortero")
            .CrearAsync(_client);

        // Act - filtrar por IdEspecialidad=2 (Albañilería)
        var response = await _client.GetAsync(
            $"/api/compras/requerimientos?idEspecialidad={SeedIds.EspecialidadAlbanileria}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task Get_ConIdExistente_RetornaRequerimientoConItemsYValidacionesYPuedeEditar()
    {
        // Arrange
        var id = await RequerimientoBuilder.Nuevo().CrearAsync(_client);

        // Act
        var response = await _client.GetAsync($"/api/compras/requerimientos/{id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var requerimiento = body.GetProperty("requerimiento");
        Assert.That(requerimiento.GetProperty("idRequerimiento").GetInt32(), Is.EqualTo(id));
        Assert.That(requerimiento.GetProperty("idProyecto").GetInt32(), Is.EqualTo(SeedIds.ProyectoMaytaCapacII));
        Assert.That(requerimiento.GetProperty("estado").GetString(), Is.EqualTo("Registrado"));
        Assert.That(requerimiento.GetProperty("numeroRequerimiento").GetString(), Is.Not.Empty);

        var items = body.GetProperty("items");
        Assert.That(items.GetArrayLength(), Is.EqualTo(1));
        Assert.That(items[0].GetProperty("idMaterial").GetInt32(), Is.EqualTo(IdMaterialAlbanileria));
        Assert.That(items[0].GetProperty("cantidad").GetDecimal(), Is.EqualTo(10m));

        // Sin validaciones previas, debe ser un array vacío
        var validaciones = body.GetProperty("validaciones");
        Assert.That(validaciones.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(validaciones.GetArrayLength(), Is.EqualTo(0));

        // puedeEditar es true solo si estado=Registrado y no tiene OC
        Assert.That(body.GetProperty("puedeEditar").GetBoolean(), Is.True,
            "Un requerimiento recién creado en estado 'Registrado' debe poder editarse.");
    }

    [Test]
    public async Task Get_ConIdInexistente_RetornaNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/compras/requerimientos/99999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Crear_ConDatosValidos_RetornaOkEPersisteCabeceraYDetalle()
    {
        // Arrange
        var builder = RequerimientoBuilder.Nuevo()
            .ConDescripcion("Requerimiento de prueba explícito")
            .ConObservacion("Observación explícita");

        // Act
        var response = await _client.PostAsJsonAsync("/api/compras/requerimientos", builder.Build());

        // Assert - 1: HTTP
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("idRequerimiento").GetInt32();
        Assert.That(id, Is.GreaterThan(0));

        // Assert - 2: BD - cabecera persistida con el NumeroRequerimiento enviado
        var dtoEnviado = builder.Build();
        var cabecera = (await DbHelpers.QueryAsync<ReqRow>(
            "SELECT NumeroRequerimiento AS Numero, Estado, Descripcion, Observacion " +
            "FROM compras.Requerimiento WHERE IdRequerimiento = @id",
            new { id })).Single();
        Assert.That(cabecera.Numero, Is.EqualTo(dtoEnviado.NumeroRequerimiento));
        Assert.That(cabecera.Estado, Is.EqualTo("Registrado"));
        Assert.That(cabecera.Descripcion, Is.EqualTo(dtoEnviado.Descripcion));
        Assert.That(cabecera.Observacion, Is.EqualTo(dtoEnviado.Observacion));

        // Assert - 3: BD - detalle persistido vía TVP
        var totalDetalles = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM compras.RequerimientoDetalle WHERE IdRequerimiento = @id",
            new { id });
        Assert.That(totalDetalles, Is.EqualTo(1));
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaOkYSobreescribeItems()
    {
        // Arrange - crear con 1 item
        var id = await RequerimientoBuilder.Nuevo().CrearAsync(_client);

        // Act - PUT con 2 items diferentes
        var updateDto = new RequerimientoUpdateDto
        {
            NumeroRequerimiento = Guid.NewGuid().ToString("N").Substring(0, 8),
            FechaRequerimiento = DateTime.Today,
            IdEspecialidad = SeedIds.EspecialidadAlbanileria,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            IdUsuarioSolicitante = SeedIds.IngenieroId,
            Observacion = "Requerimiento actualizado",
            Items = new List<RequerimientoDetalleUpdateDto>
            {
                new() { IdMaterial = IdMaterialAlbanileria, Cantidad = 20m, Observacion = "Item 1 actualizado" },
                new() { IdMaterial = IdMaterialCasco, Cantidad = 30m, Observacion = "Item 2 nuevo" }
            }
        };
        var response = await _client.PutAsJsonAsync($"/api/compras/requerimientos/{id}", updateDto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var totalDetalles = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM compras.RequerimientoDetalle WHERE IdRequerimiento = @id",
            new { id });
        Assert.That(totalDetalles, Is.EqualTo(2),
            "El Update debe reemplazar el set completo de items (borrar existentes + insertar nuevos).");
    }

    [Test]
    public async Task UpdateEstado_AValidadoAlmacen_RetornaOkYCambiaEstadoEnBD()
    {
        // Arrange
        var id = await RequerimientoBuilder.Nuevo().CrearAsync(_client);

        // Act
        var estadoDto = new RequerimientoEstadoDto
        {
            Estado = "ValidadoAlmacen",
            Observacion = "Verificado por almacén"
        };
        var response = await _client.PatchAsync(
            $"/api/compras/requerimientos/{id}/estado",
            JsonContent.Create(estadoDto));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var estadoEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Estado FROM compras.Requerimiento WHERE IdRequerimiento = @id",
            new { id });
        Assert.That(estadoEnBd, Is.EqualTo("ValidadoAlmacen"));
    }

    [Test]
    public async Task ValidarAlmacen_ConResultadoConforme_RetornaOkYRegistraValidacion()
    {
        // Arrange
        var id = await RequerimientoBuilder.Nuevo().CrearAsync(_client);

        // Act
        var validacionDto = new RequerimientoValidacionDto
        {
            IdUsuario = IdUsuarioAlmacen,
            Resultado = "Conforme",
            Observacion = "Todo en orden"
        };
        var response = await _client.PostAsJsonAsync(
            $"/api/compras/requerimientos/{id}/validacion-almacen",
            validacionDto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var validaciones = await DbHelpers.QueryAsync<ValidacionRow>(
            "SELECT IdRequerimiento, IdUsuario, Resultado " +
            "FROM compras.RequerimientoValidacion " +
            "WHERE IdRequerimiento = @id",
            new { id });
        Assert.That(validaciones, Has.Exactly(1).Items,
            "Debe existir exactamente una validación registrada.");
        var v = validaciones.Single();
        Assert.That(v.IdRequerimiento, Is.EqualTo(id));
        Assert.That(v.IdUsuario, Is.EqualTo(IdUsuarioAlmacen));
        Assert.That(v.Resultado, Is.EqualTo("Conforme"));
    }

    [Test]
    public async Task Crear_LlamaDosVecesMismoBody_CreaRequerimientosConNumerosAutoGenerados()
    {
        // Arrange - POST NO es idempotente: cada llamada crea un nuevo requerimiento.
        // Si el NumeroRequerimiento enviado ya existe, el repo calcula el siguiente
        // como MAX(TRY_CONVERT(INT, NumeroRequerimiento))+1.
        var builder = RequerimientoBuilder.Nuevo().ConNumero("DUP-001");

        // Act
        var r1 = await _client.PostAsJsonAsync("/api/compras/requerimientos", builder.Build());
        var r2 = await _client.PostAsJsonAsync("/api/compras/requerimientos", builder.Build());

        // Assert - ambas son 200 y devuelven IDs distintos
        Assert.That(r1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(r2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var id1 = (await r1.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idRequerimiento").GetInt32();
        var id2 = (await r2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("idRequerimiento").GetInt32();
        Assert.That(id2, Is.Not.EqualTo(id1),
            "Cada POST debe generar un nuevo IdRequerimiento, incluso con el mismo body.");

        // Ambos requerimientos existen en BD
        var total = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM compras.Requerimiento");
        Assert.That(total, Is.EqualTo(2));
    }

    [Test]
    public async Task Update_LlamaDosVecesMismoBody_EstadoFinalConsistente()
    {
        // Arrange - PUT SÍ es idempotente: dos llamadas con el mismo body
        // deben producir el mismo estado final (mismo set de items, mismos valores).
        var id = await RequerimientoBuilder.Nuevo().CrearAsync(_client);
        var updateDto = new RequerimientoUpdateDto
        {
            NumeroRequerimiento = "REQ-IDEM-" + Guid.NewGuid().ToString("N").Substring(0, 8),
            FechaRequerimiento = DateTime.Today,
            IdEspecialidad = SeedIds.EspecialidadAlbanileria,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            IdUsuarioSolicitante = SeedIds.IngenieroId,
            Observacion = "Actualización idempotente",
            Items = new List<RequerimientoDetalleUpdateDto>
            {
                new() { IdMaterial = IdMaterialAlbanileria, Cantidad = 99m, Observacion = "Item fijo" }
            }
        };

        // Act - llamar dos veces con el mismo body
        var r1 = await _client.PutAsJsonAsync($"/api/compras/requerimientos/{id}", updateDto);
        var r2 = await _client.PutAsJsonAsync($"/api/compras/requerimientos/{id}", updateDto);

        // Assert - ambas son 200
        Assert.That(r1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(r2.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Solo existe 1 fila de detalle con el material y cantidad esperados
        var filas = await DbHelpers.QueryAsync<DetalleRow>(
            "SELECT IdMaterial, Cantidad FROM compras.RequerimientoDetalle " +
            "WHERE IdRequerimiento = @id",
            new { id });
        Assert.That(filas, Has.Exactly(1).Items,
            "PUT idempotente: el 2do update no debe duplicar el item.");
        Assert.That(filas.Single().IdMaterial, Is.EqualTo(IdMaterialAlbanileria));
        Assert.That(filas.Single().Cantidad, Is.EqualTo(99m));
    }

    // --- Tipos de proyección para Dapper (records inmutables) ---
    private record ReqRow(string Numero, string Estado, string? Descripcion, string? Observacion);
    private record ValidacionRow(int IdRequerimiento, int IdUsuario, string Resultado);
    private record DetalleRow(int IdMaterial, decimal Cantidad);
}
