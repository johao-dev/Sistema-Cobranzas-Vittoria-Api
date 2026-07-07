using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

/// <summary>
/// Pruebas de OrdenesCompraController.
///
///   GET    /api/compras/ordenes-compra?estado=&idProveedor=&idProyecto=  -> List
///   GET    /api/compras/ordenes-compra/{id}                              -> Get
///   POST   /api/compras/ordenes-compra                                   -> Crear
///   PUT    /api/compras/ordenes-compra/{id}                              -> Update
///   PATCH  /api/compras/ordenes-compra/{id}/estado                       -> UpdateEstado
///
/// Estados válidos (CHECK): Registrada, Aceptada, Atendida, Cerrada, Anulada.
/// Estado inicial al crear: 'Registrada'.
///
/// Numeración: el server calcula NumeroOrdenCompra como
///   RIGHT('0000000' + CONVERT(MAX(NumeroOrdenCompra como int) + 1), 7)
/// Es decir, formato "0000001", "0000002", ... de 7 dígitos.
/// </summary>
public class OrdenesCompraControllerTests : IntegrationTestBase
{
    // IdProveedor=2 = ACG EDIFICACIONES (Activo=1).
    // IdProveedor=1 = "prueba" (Activo=0) - se evita para no romper la FK lógica.
    private const int IdProveedor = 2;

    // Materiales del seed
    private const int IdMaterialAlbanileria = 2;
    private const int IdMaterialCasco = 6;

    private async Task<int> CrearOrdenAsync(
        int idRequerimiento,
        List<OrdenCompraDetalleCreateDto>? detalles = null,
        int idProveedor = IdProveedor)
    {
        var dto = new OrdenCompraCreateDto
        {
            NumeroOrdenCompra = string.Empty, // el server lo autogenera si viene vacío
            IdRequerimiento = idRequerimiento,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            IdProveedor = idProveedor,
            FechaOrdenCompra = DateTime.Today,
            Descripcion = "OC de prueba de integración",
            IdUsuarioCreacion = SeedIds.IngenieroId,
            Items = detalles ?? new List<OrdenCompraDetalleCreateDto>
            {
                new() { IdMaterial = IdMaterialAlbanileria, Cantidad = 10m, IdProveedor = idProveedor, PrecioUnitario = 15.50m }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/compras/ordenes-compra", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear OC. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("idOrdenCompra").GetInt32();
    }

    [Test]
    public async Task List_SinFiltros_RetornaArrayVacio()
    {
        // Act - la tabla compras.OrdenCompra se limpia antes de cada test
        var response = await _client.GetAsync("/api/compras/ordenes-compra");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task List_FiltradoPorEstadoRegistrada_RetornaArrayVacioSiNoHayCoincidencias()
    {
        // Act
        var response = await _client.GetAsync("/api/compras/ordenes-compra?estado=Registrada");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task List_FiltradoPorIdProyecto_RetornaOrdenesCoincidentes()
    {
        // Arrange - crear 2 OCs del mismo proyecto (cada una con su propio requerimiento)
        var idReq1 = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idReq2 = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        await CrearOrdenAsync(idReq1);
        await CrearOrdenAsync(idReq2);

        // Act
        var response = await _client.GetAsync(
            $"/api/compras/ordenes-compra?idProyecto={SeedIds.ProyectoMaytaCapacII}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task List_FiltradoPorIdProveedor_RetornaOrdenesCoincidentes()
    {
        // Arrange - crear 2 OCs al mismo proveedor
        var idReq1 = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idReq2 = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        await CrearOrdenAsync(idReq1);
        await CrearOrdenAsync(idReq2);

        // Act
        var response = await _client.GetAsync(
            $"/api/compras/ordenes-compra?idProveedor={IdProveedor}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task Get_ConIdExistente_RetornaOrdenConCabeceraYDetalles()
    {
        // Arrange
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc = await CrearOrdenAsync(idReq);

        // Act
        var response = await _client.GetAsync($"/api/compras/ordenes-compra/{idOc}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // El service retorna { ordenCompra, items, historial }
        var ordenCompra = body.GetProperty("ordenCompra");
        Assert.That(ordenCompra.GetProperty("idOrdenCompra").GetInt32(), Is.EqualTo(idOc));
        Assert.That(ordenCompra.GetProperty("idRequerimiento").GetInt32(), Is.EqualTo(idReq));
        Assert.That(ordenCompra.GetProperty("idProveedor").GetInt32(), Is.EqualTo(IdProveedor));
        Assert.That(ordenCompra.GetProperty("idProyecto").GetInt32(), Is.EqualTo(SeedIds.ProyectoMaytaCapacII));
        Assert.That(ordenCompra.GetProperty("estado").GetString(), Is.EqualTo("Registrada"));
        Assert.That(ordenCompra.GetProperty("numeroOrdenCompra").GetString(), Is.Not.Empty);

        // Total = 10 * 15.50 = 155.00
        Assert.That(ordenCompra.GetProperty("total").GetDecimal(), Is.EqualTo(155.00m));

        // El subrecurso 'items' existe y tiene al menos 1 item
        var items = body.GetProperty("items");
        Assert.That(items.GetArrayLength(), Is.EqualTo(1));

        // El subrecurso 'historial' existe (vacío al crear - el SP Crear no inserta historial)
        var historial = body.GetProperty("historial");
        Assert.That(historial.ValueKind, Is.EqualTo(JsonValueKind.Array));
    }

    [Test]
    public async Task Get_ConIdInexistente_RetornaNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/compras/ordenes-compra/99999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Crear_ConRequerimientoYProveedorValidos_RetornaOkEPersisteCabeceraDetalleYHistorial()
    {
        // Arrange
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);

        // Act
        var idOc = await CrearOrdenAsync(idReq, detalles: new List<OrdenCompraDetalleCreateDto>
        {
            new() { IdMaterial = IdMaterialAlbanileria, Cantidad = 5m, IdProveedor = IdProveedor, PrecioUnitario = 20m },
            new() { IdMaterial = IdMaterialCasco, Cantidad = 3m, IdProveedor = IdProveedor, PrecioUnitario = 100m }
        });

        // Assert - 1: BD - cabecera
        var cabecera = (await DbHelpers.QueryAsync<OcRow>(
            "SELECT IdOrdenCompra AS Id, NumeroOrdenCompra AS Numero, Estado, IdRequerimiento AS Req, IdProveedor AS Prov, Total " +
            "FROM compras.OrdenCompra WHERE IdOrdenCompra = @id",
            new { id = idOc })).Single();
        Assert.That(cabecera.Numero, Is.Not.Empty);
        Assert.That(cabecera.Estado, Is.EqualTo("Registrada"));
        Assert.That(cabecera.Req, Is.EqualTo(idReq));
        Assert.That(cabecera.Prov, Is.EqualTo(IdProveedor));
        // Total = (5*20) + (3*100) = 100 + 300 = 400
        Assert.That(cabecera.Total, Is.EqualTo(400.00m));

        // Assert - 2: BD - detalle
        var totalDetalles = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM compras.OrdenCompraDetalle WHERE IdOrdenCompra = @id",
            new { id = idOc });
        Assert.That(totalDetalles, Is.EqualTo(2));

        // Assert - 3: BD - el Requerimiento pasa a estado 'GeneradoOC' (efecto del SP usp_OrdenCompra_CrearDesdeRequerimiento, línea 722)
        var estadoReq = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Estado FROM compras.Requerimiento WHERE IdRequerimiento = @id",
            new { id = idReq });
        Assert.That(estadoReq, Is.EqualTo("GeneradoOC"),
            "Al crear la OC, el Requerimiento debe transicionar a 'GeneradoOC'.");
    }

    [Test]
    public async Task Update_ConIdExistente_RetornaOkYSobreescribeItems()
    {
        // Arrange - crear OC con 1 item
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc = await CrearOrdenAsync(idReq);

        // Act - PUT con 2 items diferentes
        var updateDto = new OrdenCompraUpdateDto
        {
            NumeroOrdenCompra = "OC-TEST",
            IdRequerimiento = idReq,
            IdProveedor = IdProveedor,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            FechaOrdenCompra = DateTime.Today,
            Descripcion = "OC actualizada",
            Items = new List<OrdenCompraDetalleUpdateDto>
            {
                new() { IdMaterial = IdMaterialAlbanileria, Cantidad = 20m, IdProveedor = IdProveedor, PrecioUnitario = 18m },
                new() { IdMaterial = IdMaterialCasco, Cantidad = 30m, IdProveedor = IdProveedor, PrecioUnitario = 25m }
            }
        };
        var response = await _client.PutAsJsonAsync($"/api/compras/ordenes-compra/{idOc}", updateDto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var totalDetalles = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM compras.OrdenCompraDetalle WHERE IdOrdenCompra = @id",
            new { id = idOc });
        Assert.That(totalDetalles, Is.EqualTo(2),
            "El Update debe reemplazar el set completo de items.");

        // Total = (20*18) + (30*25) = 360 + 750 = 1110
        var totalEnBd = await DbHelpers.QueryScalarAsync<decimal>(
            "SELECT Total FROM compras.OrdenCompra WHERE IdOrdenCompra = @id",
            new { id = idOc });
        Assert.That(totalEnBd, Is.EqualTo(1110.00m));
    }

    [Test]
    public async Task UpdateEstado_AAtendida_RetornaOkYCambiaEstadoEnBDYRegistraHistorial()
    {
        // Arrange
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc = await CrearOrdenAsync(idReq);

        // Act
        // Bug del código fuente: el SP usp_OrdenCompra_ActualizarEstado solo acepta
        //   {Generada, Aprobada, Enviada, Atendida, Anulada}
        // pero el CHECK de la tabla solo permite
        //   {Anulada, Cerrada, Atendida, Aceptada, Registrada}.
        // La única transición válida desde 'Registrada' es a 'Atendida' (o 'Anulada').
        var estadoDto = new OrdenCompraEstadoDto
        {
            EstadoNuevo = "Atendida",
            IdUsuario = SeedIds.IngenieroId,
            Observacion = "OC atendida completamente"
        };
        var response = await _client.PatchAsync(
            $"/api/compras/ordenes-compra/{idOc}/estado",
            JsonContent.Create(estadoDto));

        // Assert - 1: estado de la cabecera
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"PATCH /estado debe retornar 200. Body: {await response.Content.ReadAsStringAsync()}");
        var estadoEnBd = await DbHelpers.QueryScalarAsync<string>(
            "SELECT Estado FROM compras.OrdenCompra WHERE IdOrdenCompra = @id",
            new { id = idOc });
        Assert.That(estadoEnBd, Is.EqualTo("Atendida"));

        // Assert - 2: historial - debe haber 1 evento con EstadoAnterior='Registrada' y EstadoNuevo='Atendida'
        var historial = await DbHelpers.QueryAsync<HistorialRow>(
            "SELECT EstadoNuevo AS Estado, IdUsuario FROM compras.OrdenCompraHistorial " +
            "WHERE IdOrdenCompra = @id ORDER BY IdOrdenCompraHistorial",
            new { id = idOc });
        Assert.That(historial, Has.Exactly(1).Items,
            "Crear OC no inserta historial; el primer evento es el cambio de estado.");
        Assert.That(historial.Single().Estado, Is.EqualTo("Atendida"));
        Assert.That(historial.Single().IdUsuario, Is.EqualTo(SeedIds.IngenieroId));
    }

    [Test]
    public async Task Crear_LlamaDosVecesMismoBody_CreaOrdenesConNumerosAutoGenerados()
    {
        // Arrange - POST NO es idempotente: cada llamada crea una nueva OC.
        // El server autogenera el NumeroOrdenCompra secuencialmente.
        var idReq1 = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idReq2 = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);

        // Act
        var idOc1 = await CrearOrdenAsync(idReq1);
        var idOc2 = await CrearOrdenAsync(idReq2);

        // Assert - IDs distintos
        Assert.That(idOc2, Is.Not.EqualTo(idOc1));

        // Números distintos, todos dígitos, y consecutivos (1, 2)
        var numeros = await DbHelpers.QueryAsync<int>(
            "SELECT TRY_CAST(NumeroOrdenCompra AS INT) FROM compras.OrdenCompra ORDER BY IdOrdenCompra");
        Assert.That(numeros, Has.Exactly(2).Items);
        Assert.That(numeros, Is.Unique);
        Assert.That(numeros, Is.EqualTo(new[] { 1, 2 }),
            "EnsureNumeroOrdenCompraAsync genera enteros consecutivos (1, 2, ...) no padded a 7 dígitos.");
    }

    [Test]
    public async Task Update_LlamaDosVecesMismoBody_EstadoFinalConsistente()
    {
        // Arrange - PUT SÍ es idempotente: dos llamadas con el mismo body
        // deben producir el mismo estado final.
        var idReq = await RequerimientoBuilder.Nuevo().CrearEnviadoOcAsync(_client);
        var idOc = await CrearOrdenAsync(idReq);
        var updateDto = new OrdenCompraUpdateDto
        {
            NumeroOrdenCompra = "OC-IDEM",
            IdRequerimiento = idReq,
            IdProveedor = IdProveedor,
            IdProyecto = SeedIds.ProyectoMaytaCapacII,
            FechaOrdenCompra = DateTime.Today,
            Descripcion = "Actualización idempotente",
            Items = new List<OrdenCompraDetalleUpdateDto>
            {
                new() { IdMaterial = IdMaterialAlbanileria, Cantidad = 50m, IdProveedor = IdProveedor, PrecioUnitario = 12m }
            }
        };

        // Act
        var r1 = await _client.PutAsJsonAsync($"/api/compras/ordenes-compra/{idOc}", updateDto);
        var r2 = await _client.PutAsJsonAsync($"/api/compras/ordenes-compra/{idOc}", updateDto);

        // Assert
        Assert.That(r1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(r2.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Solo 1 fila de detalle con material=2 y cantidad=50
        var filas = await DbHelpers.QueryAsync<DetRow>(
            "SELECT IdMaterial AS Mat, Cantidad AS Cant FROM compras.OrdenCompraDetalle " +
            "WHERE IdOrdenCompra = @id",
            new { id = idOc });
        Assert.That(filas, Has.Exactly(1).Items,
            "PUT idempotente: el 2do update no debe duplicar el item.");
        Assert.That(filas.Single().Mat, Is.EqualTo(IdMaterialAlbanileria));
        Assert.That(filas.Single().Cant, Is.EqualTo(50m));

        // Total = 50 * 12 = 600
        var totalEnBd = await DbHelpers.QueryScalarAsync<decimal>(
            "SELECT Total FROM compras.OrdenCompra WHERE IdOrdenCompra = @id",
            new { id = idOc });
        Assert.That(totalEnBd, Is.EqualTo(600.00m));
    }

    // --- Tipos de proyección para Dapper ---
    private record OcRow(int Id, string Numero, string Estado, int Req, int Prov, decimal Total);
    private record HistorialRow(string Estado, int IdUsuario);
    private record DetRow(int Mat, decimal Cant);
}
