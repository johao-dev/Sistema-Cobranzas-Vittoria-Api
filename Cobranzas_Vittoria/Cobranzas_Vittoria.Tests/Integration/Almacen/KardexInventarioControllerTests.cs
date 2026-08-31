using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Application.Inventario.Dtos;
using Cobranzas_Vittoria.Tests;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Almacen;

/// <summary>
/// Pruebas de <c>KardexInventarioController</c> (modulo Inventario / Kardex manual).
///
/// <para>
/// <b>Endpoints cubiertos</b> (todos bajo <c>api/almacen/kardex</c>):
/// <list type="bullet">
///   <item>GET    /entradas                                -> ListarEntradas</item>
///   <item>POST   /entradas                                -> RegistrarEntrada</item>
///   <item>PUT    /entradas/{id}                           -> ActualizarEntrada</item>
///   <item>DELETE /entradas/{id}                           -> EliminarEntrada</item>
///   <item>GET    /salidas                                 -> ListarSalidas</item>
///   <item>POST   /salidas                                 -> RegistrarSalida</item>
///   <item>PUT    /salidas/{id}                            -> ActualizarSalida</item>
///   <item>DELETE /salidas/{id}                            -> EliminarSalida</item>
///   <item>GET    /stock-actual                            -> ListarStockActual</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Reglas de negocio validadas en estos tests</b>:
/// <list type="bullet">
///   <item>Una entrada con cantidad 10 deja Stock=10 en KardexStock.</item>
///   <item>Una salida que pide mas del stock disponible responde 422 con codigo STOCK_INSUFICIENTE.</item>
///   <item>PUT con idRuta != idCuerpo responde 400 con codigo ID_RUTA_INCONSISTENTE.</item>
///   <item>PUT/DELETE con id inexistente responde 404 con codigo KARDEX_NO_ENCONTRADO.</item>
///   <item>Eliminar una entrada repone el stock (TotalEntrada y Stock se decrementan).</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Coexistencia con el legacy</b>: el <c>KardexController</c> legacy
/// expone <c>GET /movimientos</c> y <c>POST /salidas-manuales</c>. Estos
/// tests no tocan esas rutas; solo el nuevo <c>KardexInventarioController</c>.
/// </para>
/// </summary>
public class KardexInventarioControllerTests : IntegrationTestBase
{
    // IDs canonicos del seed (V1_1_0__SeedData.sql).
    // - IdMaterial=2 -> "MORTERO LISTO" (Albañileria, IdEspecialidad=2)
    // - IdMaterial=11 -> "DISCO DE CORTE PARA ACERO DE 4"" (Casco, IdEspecialidad=4)
    // - IdProveedor=2 -> "ACG EDIFICACIONES EIRL" (Activo=1)
    private const int IdMaterialAlbanileria = 2;
    private const int IdMaterialCasco = 11; // Material #11 de la seed (Casco)
    private const int IdEspecialidadAlbanileria = SeedIds.EspecialidadAlbanileria;
    private const int IdEspecialidadCasco = SeedIds.EspecialidadCasco;
    private const int IdProveedor = 2;
    private const int IdProyecto = SeedIds.ProyectoMaytaCapacII;

    // ============================================================================
    // Helpers
    // ============================================================================

    private static KardexEntradaCreateDto EntradaValida(
        decimal cantidad = 10m,
        int idMaterial = IdMaterialAlbanileria,
        int idEspecialidad = IdEspecialidadAlbanileria)
        => new()
        {
            IdKardexEntrada = null,
            IdEspecialidad = idEspecialidad,
            IdMaterial = idMaterial,
            IdProveedor = IdProveedor,
            IdProyecto = IdProyecto,
            NumeroDocumento = "F001-TEST",
            Fecha = new DateOnly(2026, 1, 15),
            Cantidad = cantidad,
            Observacion = "Entrada de prueba"
        };

    private static KardexSalidaCreateDto SalidaValida(int idMaterial = IdMaterialAlbanileria, decimal cantidad = 3m, int? idEspecialidad = null)
        => new()
        {
            IdKardexSalida = null,
            IdEspecialidad = idEspecialidad ?? IdEspecialidadAlbanileria,
            // IdProyecto obligatorio para entradas y salidas (etiqueta). A partir
            // de V1_4_1 el stock es global por (IdMaterial, IdEspecialidad), asi que
            // la salida consume del mismo stock independientemente del proyecto.
            IdProyecto = IdProyecto,
            NumeroDocumento = "S001-TEST",
            Fecha = new DateOnly(2026, 1, 16),
            Solicitante = "Ingeniero de prueba",
            Observacion = "Salida de prueba",
            Items = new List<KardexSalidaItemCreateDto>
            {
                new() { IdMaterial = idMaterial, Cantidad = cantidad, Observacion = "Item 1" }
            }
        };

    /// <summary>
    /// Crea una entrada valida via API y devuelve el IdKardexEntrada.
    /// </summary>
    private async Task<int> CrearEntradaAsync(decimal cantidad = 10m, int idMaterial = IdMaterialAlbanileria, int idEspecialidad = IdEspecialidadAlbanileria)
    {
        var dto = EntradaValida(cantidad, idMaterial, idEspecialidad);
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/entradas", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear entrada. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return JsonHelpers.GetInt32(body, "idKardexEntrada");
    }

    /// <summary>
    /// Crea una salida valida via API y devuelve el IdKardexSalida.
    /// <para>
    /// <b>Por que parsea como array</b>: el SP <c>usp_KardexSalida_Registrar</c>
    /// devuelve 1 fila por cada item del detalle (mismo patron que el GET),
    /// por lo que la respuesta de POST es un array. Se toma el <c>idKardexSalida</c>
    /// de la primera fila (todos los items de una misma salida comparten ese id).
    /// </para>
    /// </summary>
    private async Task<int> CrearSalidaAsync(decimal cantidad = 3m, int idMaterial = IdMaterialAlbanileria, int? idEspecialidad = null)
    {
        var dto = SalidaValida(idMaterial, cantidad, idEspecialidad);
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas", dto);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup falló al crear salida. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array),
            "Se esperaba un array (1 fila por item).");
        Assert.That(body.GetArrayLength(), Is.GreaterThan(0),
            "El array de respuesta esta vacio.");
        return JsonHelpers.GetInt32(body[0], "idKardexSalida");
    }

    // ============================================================================
    // GET /entradas
    // ============================================================================

    [Test]
    public async Task ListarEntradas_SinDatos_RetornaArrayVacio()
    {
        var response = await _client.GetAsync("/api/almacen/kardex/entradas");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task ListarEntradas_ConUnaEntrada_RetornaLaFila()
    {
        await CrearEntradaAsync(cantidad: 25m);

        var response = await _client.GetAsync("/api/almacen/kardex/entradas");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));
        Assert.That(JsonHelpers.GetInt32(body[0], "idKardexEntrada"), Is.GreaterThan(0));
        Assert.That(JsonHelpers.GetInt32(body[0], "idMaterial"), Is.EqualTo(IdMaterialAlbanileria));
        Assert.That(JsonHelpers.GetDecimal(body[0], "cantidad"), Is.EqualTo(25m));
    }

    [Test]
    public async Task ListarEntradas_FiltroPorIdEspecialidad_AplicaCorrectamente()
    {
        // Arrange: una entrada de Albañileria y otra de Estructura.
        // IdMaterial=11 pertenece a la especialidad 4 (Estructura); usar
        // IdEspecialidad=2 (Albañileria) haria fallar la validacion FK.
        await CrearEntradaAsync(cantidad: 5m, idMaterial: IdMaterialAlbanileria, idEspecialidad: IdEspecialidadAlbanileria);
        await CrearEntradaAsync(cantidad: 7m, idMaterial: IdMaterialCasco, idEspecialidad: IdEspecialidadCasco);

        // Act
        var response = await _client.GetAsync(
            $"/api/almacen/kardex/entradas?idEspecialidad={IdEspecialidadAlbanileria}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));
        Assert.That(JsonHelpers.GetInt32(body[0], "idEspecialidad"), Is.EqualTo(IdEspecialidadAlbanileria));
    }

    // ============================================================================
    // POST /entradas
    // ============================================================================

    [Test]
    public async Task RegistrarEntrada_Valida_Retorna200ConIdAsignado()
    {
        var dto = EntradaValida(cantidad: 12m);

        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/entradas", dto);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = JsonHelpers.GetInt32(body, "idKardexEntrada");
        Assert.That(id, Is.GreaterThan(0));
        Assert.That(JsonHelpers.GetInt32(body, "idMaterial"), Is.EqualTo(IdMaterialAlbanileria));
        Assert.That(JsonHelpers.GetDecimal(body, "cantidad"), Is.EqualTo(12m));
    }

    [Test]
    public async Task RegistrarEntrada_CamposRequeridosVacios_Retorna422ConCodigoYDetalles()
    {
        // Arrange: dto con idMaterial=0 y cantidad negativa
        var dto = new KardexEntradaCreateDto
        {
            IdEspecialidad = 0,
            IdMaterial = 0,
            Cantidad = -1m,
            Fecha = default, // 0001-01-01
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/entradas", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));
        var errores = body.GetProperty("errores");
        Assert.That(errores.GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    public async Task RegistrarEntrada_MaterialInexistente_Retorna422()
    {
        var dto = EntradaValida();
        dto.IdMaterial = 99_999; // No existe en maestra.Material

        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/entradas", dto);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));
    }

    // ============================================================================
    // PUT /entradas/{id}
    // ============================================================================

    [Test]
    public async Task ActualizarEntrada_IdConsistente_Retorna200ConFilaActualizada()
    {
        var id = await CrearEntradaAsync(cantidad: 20m);

        var dto = EntradaValida(cantidad: 30m);
        dto.IdKardexEntrada = id;

        var response = await _client.PutAsJsonAsync($"/api/almacen/kardex/entradas/{id}", dto);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetInt32(body, "idKardexEntrada"), Is.EqualTo(id));
        Assert.That(JsonHelpers.GetDecimal(body, "cantidad"), Is.EqualTo(30m));
    }

    [Test]
    public async Task ActualizarEntrada_IdRutaInconsistente_Retorna400IdRutaInconsistente()
    {
        var id1 = await CrearEntradaAsync(cantidad: 10m);
        var id2 = await CrearEntradaAsync(cantidad: 20m);
        Assert.That(id2, Is.Not.EqualTo(id1));

        var dto = EntradaValida();
        dto.IdKardexEntrada = id2; // dice id2 pero la ruta es id1

        var response = await _client.PutAsJsonAsync($"/api/almacen/kardex/entradas/{id1}", dto);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("ID_RUTA_INCONSISTENTE"));
    }

    [Test]
    public async Task ActualizarEntrada_IdInexistente_Retorna404KardexNoEncontrado()
    {
        var dto = EntradaValida();
        dto.IdKardexEntrada = 99_999;

        var response = await _client.PutAsJsonAsync("/api/almacen/kardex/entradas/99999", dto);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("KARDEX_NO_ENCONTRADO"));
    }

    // ============================================================================
    // DELETE /entradas/{id}
    // ============================================================================

    [Test]
    public async Task EliminarEntrada_Existente_Retorna200()
    {
        var id = await CrearEntradaAsync(cantidad: 5m);

        var response = await _client.DeleteAsync($"/api/almacen/kardex/entradas/{id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Verificar que ya no aparece en el listado
        var listResponse = await _client.GetAsync("/api/almacen/kardex/entradas");
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(list.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task EliminarEntrada_Inexistente_Retorna404KardexNoEncontrado()
    {
        var response = await _client.DeleteAsync("/api/almacen/kardex/entradas/99999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("KARDEX_NO_ENCONTRADO"));
    }

    [Test]
    public async Task EliminarEntrada_RestaStockADescontarDelStockActual()
    {
        // Arrange: una entrada de 10 y luego una salida de 4 (Stock=6)
        await CrearEntradaAsync(cantidad: 10m);
        await CrearSalidaAsync(cantidad: 4m);

        var stockAntes = await ObtenerStockAsync(IdMaterialAlbanileria);
        Assert.That(stockAntes, Is.EqualTo(6m));

        // Act: eliminar la entrada. El SP descuenta su cantidad de KardexStock.
        // Como no hay OTRA entrada que respalde, el stock quedaria en -4 -> 51111.
        // Primero registramos OTRA entrada de 10 para tener 20-4=16 antes de eliminar
        // la primera (queda 10-4=6, sin inconsistencia).
        var idSegunda = await CrearEntradaAsync(cantidad: 10m);
        Assert.That(await ObtenerStockAsync(IdMaterialAlbanileria), Is.EqualTo(16m));

        var response = await _client.DeleteAsync($"/api/almacen/kardex/entradas/{idSegunda}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await ObtenerStockAsync(IdMaterialAlbanileria), Is.EqualTo(6m));
    }

    [Test]
    public async Task EliminarEntrada_GeneraStockNegativo_Retorna422StockInconsistente()
    {
        // Arrange: entrada de 5, salida de 3 (Stock=2)
        var id = await CrearEntradaAsync(cantidad: 5m);
        await CrearSalidaAsync(cantidad: 3m);
        Assert.That(await ObtenerStockAsync(IdMaterialAlbanileria), Is.EqualTo(2m));

        // Act: eliminar la entrada dejaria Stock=-3.
        var response = await _client.DeleteAsync($"/api/almacen/kardex/entradas/{id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));
        // El codigo del error especifico debe ser STOCK_INCONSISTENTE_AL_ELIMINAR
        var errores = body.GetProperty("errores");
        var codigos = errores.EnumerateArray()
            .Select(e => JsonHelpers.GetString(e, "codigoError"))
            .ToList();
        Assert.That(codigos, Does.Contain("STOCK_INCONSISTENTE_AL_ELIMINAR"));
    }

    private async Task<decimal> ObtenerStockAsync(int idMaterial)
    {
        var response = await _client.GetAsync(
            $"/api/almacen/kardex/stock-actual?idEspecialidad={IdEspecialidadAlbanileria}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var fila in body.EnumerateArray())
        {
            if (JsonHelpers.GetInt32(fila, "idMaterial") == idMaterial)
            {
                return JsonHelpers.GetDecimal(fila, "stock");
            }
        }
        return 0m;
    }

    // ============================================================================
    // GET /salidas
    // ============================================================================

    [Test]
    public async Task ListarSalidas_SinDatos_RetornaArrayVacio()
    {
        var response = await _client.GetAsync("/api/almacen/kardex/salidas");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task ListarSalidas_ConUnaSalida_RetornaFilaPorItem()
    {
        // Arrange: entradas de los 2 materiales para tener stock, y una salida
        // con 2 items. Materiales 2-5 son de Albañileria segun la seed
        // (V1_1_0__SeedData.sql). El validador exige que cada item pertenezca
        // a la especialidad de la cabecera, por lo que no se pueden mezclar
        // especialidades en una misma salida.
        //
        // El stock es global por (IdMaterial, IdEspecialidad). Las entradas de
        // prueba usan IdProyecto=10 (Mayta Capac II), pero la salida puede usar
        // cualquier proyecto (o ninguno) y consume del mismo stock global.
        await CrearEntradaAsync(cantidad: 5m, idMaterial: IdMaterialAlbanileria);
        await CrearEntradaAsync(cantidad: 5m, idMaterial: 3 /* PLASTICO AZUL */);
        var dto = new KardexSalidaCreateDto
        {
            IdEspecialidad = IdEspecialidadAlbanileria,
            IdProyecto = IdProyecto,
            Fecha = new DateOnly(2026, 1, 17),
            Solicitante = "Test multi-item",
            Items = new List<KardexSalidaItemCreateDto>
            {
                new() { IdMaterial = IdMaterialAlbanileria, Cantidad = 1m },
                new() { IdMaterial = 3 /* PLASTICO AZUL, Albañileria */, Cantidad = 2m }
            }
        };
        var post = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas", dto);
        Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Setup fallo. Body: {await post.Content.ReadAsStringAsync()}");

        var response = await _client.GetAsync("/api/almacen/kardex/salidas");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // El SP devuelve una fila por item (cabecera repetida).
        Assert.That(body.GetArrayLength(), Is.EqualTo(2));
    }

    // ============================================================================
    // POST /salidas
    // ============================================================================

    [Test]
    public async Task RegistrarSalida_StockSuficiente_Retorna200YAplicaDescuentoEnStock()
    {
        // Arrange: entrada de 10
        await CrearEntradaAsync(cantidad: 10m);

        // Act
        var dto = SalidaValida(cantidad: 4m);
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        // POST /salidas devuelve un array (1 fila por item); verificamos la primera.
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(JsonHelpers.GetInt32(body[0], "idKardexSalida"), Is.GreaterThan(0));
        Assert.That(await ObtenerStockAsync(IdMaterialAlbanileria), Is.EqualTo(6m));
    }

    [Test]
    public async Task RegistrarSalida_DesdeOtroProyecto_ConsumeStockGlobal_Retorna200()
    {
        // Arrange: entrada con IdProyecto=10. El stock es global.
        await CrearEntradaAsync(cantidad: 10m);

        // Creamos un segundo proyecto valido para la salida. El seed solo trae
        // el proyecto 10, y el validador exige que el proyecto de la salida exista.
        // IdProyecto es IDENTITY, asi que usamos IDENTITY_INSERT para forzar el id 20.
        await using (var connection = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                IF NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = 20)
                BEGIN
                    SET IDENTITY_INSERT maestra.Proyecto ON;
                    INSERT INTO maestra.Proyecto (IdProyecto, NombreProyecto, Descripcion, Activo, FechaCreacion, CotizacionGeneral)
                    VALUES (20, N'Proyecto Test Secundario', N'Solo para tests', 1, GETDATE(), 0.00);
                    SET IDENTITY_INSERT maestra.Proyecto OFF;
                END";
            await cmd.ExecuteNonQueryAsync();
        }

        // Act: salida con OTRO proyecto (IdProyecto=20) debe poder consumir el mismo stock.
        var dto = SalidaValida(cantidad: 4m);
        dto.IdProyecto = 20;
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        Assert.That(await ObtenerStockAsync(IdMaterialAlbanileria), Is.EqualTo(6m));
    }

    [Test]
    public async Task RegistrarSalida_SinProyecto_Retorna422CampoRequerido()
    {
        // Act: salida sin proyecto debe fallar (el proyecto es obligatorio).
        var dto = SalidaValida(cantidad: 4m);
        dto.IdProyecto = null;
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));
        var codigos = body.GetProperty("errores").EnumerateArray()
            .Select(e => JsonHelpers.GetString(e, "codigoError"))
            .ToList();
        Assert.That(codigos, Does.Contain("CAMPO_REQUERIDO"));
    }

    [Test]
    public async Task RegistrarSalida_StockInsuficiente_Retorna422StockInsuficiente()
    {
        // Arrange: entrada de 5
        await CrearEntradaAsync(cantidad: 5m);

        // Act: pedir 10 (no hay stock)
        var dto = SalidaValida(cantidad: 10m);
        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));
        var codigos = body.GetProperty("errores").EnumerateArray()
            .Select(e => JsonHelpers.GetString(e, "codigoError"))
            .ToList();
        Assert.That(codigos, Does.Contain("STOCK_INSUFICIENTE"));
    }

    [Test]
    public async Task RegistrarSalida_SinItems_Retorna422ConCodigoGenerico()
    {
        // Arrange: dto con Items vacios (lo bloquea el validator antes del SP)
        var dto = new KardexSalidaCreateDto
        {
            IdEspecialidad = IdEspecialidadAlbanileria,
            Fecha = new DateOnly(2026, 1, 18),
            Solicitante = "Test sin items",
            Items = new List<KardexSalidaItemCreateDto>() // vacio
        };

        var response = await _client.PostAsJsonAsync("/api/almacen/kardex/salidas", dto);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    // ============================================================================
    // PUT /salidas/{id}
    // ============================================================================

    [Test]
    public async Task ActualizarSalida_ReemplazaCabeceraYItems_Retorna200()
    {
        // Arrange: entrada de 20 y salida de 5
        await CrearEntradaAsync(cantidad: 20m);
        var idSalida = await CrearSalidaAsync(cantidad: 5m);
        Assert.That(await ObtenerStockAsync(IdMaterialAlbanileria), Is.EqualTo(15m));

        // Act: reemplazar la salida por una de 3 (stock debe subir a 17)
        var dto = SalidaValida(cantidad: 3m);
        dto.IdKardexSalida = idSalida;
        var response = await _client.PutAsJsonAsync($"/api/almacen/kardex/salidas/{idSalida}", dto);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        // PUT devuelve un array (1 fila por item); verificamos la primera fila.
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(JsonHelpers.GetInt32(body[0], "idKardexSalida"), Is.EqualTo(idSalida));
        Assert.That(await ObtenerStockAsync(IdMaterialAlbanileria), Is.EqualTo(17m));
    }

    [Test]
    public async Task ActualizarSalida_IdRutaInconsistente_Retorna400IdRutaInconsistente()
    {
        // Arrange: entrada de 10 para tener stock, y 2 salidas de 2 y 3.
        await CrearEntradaAsync(cantidad: 10m);
        var id1 = await CrearSalidaAsync(cantidad: 2m);
        var id2 = await CrearSalidaAsync(cantidad: 3m);

        var dto = SalidaValida();
        dto.IdKardexSalida = id2; // id del cuerpo distinto al de la ruta

        var response = await _client.PutAsJsonAsync($"/api/almacen/kardex/salidas/{id1}", dto);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("ID_RUTA_INCONSISTENTE"));
    }

    [Test]
    public async Task ActualizarSalida_IdInexistente_Retorna404KardexNoEncontrado()
    {
        var dto = SalidaValida();
        dto.IdKardexSalida = 99_999;

        var response = await _client.PutAsJsonAsync("/api/almacen/kardex/salidas/99999", dto);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("KARDEX_NO_ENCONTRADO"));
    }

    // ============================================================================
    // DELETE /salidas/{id}
    // ============================================================================

    [Test]
    public async Task EliminarSalida_Existente_ReponeStock()
    {
        // Arrange: entrada de 10, salida de 3 (Stock=7)
        await CrearEntradaAsync(cantidad: 10m);
        var idSalida = await CrearSalidaAsync(cantidad: 3m);
        Assert.That(await ObtenerStockAsync(IdMaterialAlbanileria), Is.EqualTo(7m));

        // Act
        var response = await _client.DeleteAsync($"/api/almacen/kardex/salidas/{idSalida}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await ObtenerStockAsync(IdMaterialAlbanileria), Is.EqualTo(10m));
    }

    [Test]
    public async Task EliminarSalida_Inexistente_Retorna404KardexNoEncontrado()
    {
        var response = await _client.DeleteAsync("/api/almacen/kardex/salidas/99999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(JsonHelpers.GetString(body, "error"), Is.EqualTo("KARDEX_NO_ENCONTRADO"));
    }

    // ============================================================================
    // GET /stock-actual
    // ============================================================================

    [Test]
    public async Task StockActual_SinMovimientos_RetornaArrayVacio()
    {
        var response = await _client.GetAsync("/api/almacen/kardex/stock-actual");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task StockActual_DespuesDeUnaEntrada_RetornaUnaFilaConStockEsperado()
    {
        await CrearEntradaAsync(cantidad: 15m);

        var response = await _client.GetAsync("/api/almacen/kardex/stock-actual");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));
        var fila = body[0];
        Assert.That(JsonHelpers.GetInt32(fila, "idMaterial"), Is.EqualTo(IdMaterialAlbanileria));
        Assert.That(JsonHelpers.GetDecimal(fila, "stock"), Is.EqualTo(15m));
    }

    [Test]
    public async Task StockActual_DespuesDeEntradaYSalida_StockEsLaDiferencia()
    {
        await CrearEntradaAsync(cantidad: 20m);
        await CrearSalidaAsync(cantidad: 7m);

        var response = await _client.GetAsync("/api/almacen/kardex/stock-actual");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fila = body[0];
        Assert.That(JsonHelpers.GetDecimal(fila, "stock"), Is.EqualTo(13m));
        Assert.That(JsonHelpers.GetDecimal(fila, "totalEntrada"), Is.EqualTo(20m));
        Assert.That(JsonHelpers.GetDecimal(fila, "totalSalida"), Is.EqualTo(7m));
    }

    [Test]
    public async Task StockActual_FiltroPorIdEspecialidad_AplicaCorrectamente()
    {
        // Arrange: una entrada de Albañileria y otra de Casco.
        // IdMaterial=11 pertenece a la especialidad 4 (Casco); usar
        // IdEspecialidad=2 (Albañileria) haria fallar la validacion FK.
        await CrearEntradaAsync(cantidad: 5m, idMaterial: IdMaterialAlbanileria, idEspecialidad: IdEspecialidadAlbanileria);
        await CrearEntradaAsync(cantidad: 8m, idMaterial: IdMaterialCasco, idEspecialidad: IdEspecialidadCasco);

        // Act
        var response = await _client.GetAsync(
            $"/api/almacen/kardex/stock-actual?idEspecialidad={IdEspecialidadCasco}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));
        Assert.That(JsonHelpers.GetInt32(body[0], "idMaterial"), Is.EqualTo(IdMaterialCasco));
        Assert.That(JsonHelpers.GetDecimal(body[0], "stock"), Is.EqualTo(8m));
    }

    [Test]
    public async Task StockActual_FiltroPorRangoDeFechas_FiltraPorFechaUltimaMovimiento()
    {
        // Arrange: una entrada de hoy y un movimiento de KardexStock con
        // FechaUltimaMovimiento = fecha de la entrada (2026-01-15).
        await CrearEntradaAsync(cantidad: 10m);

        // Act: filtrar por fechas que incluyen la entrada
        var response = await _client.GetAsync(
            "/api/almacen/kardex/stock-actual?fechaDesde=2026-01-01&fechaHasta=2026-01-31");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));

        // Filtrar fechas que excluyen la entrada
        var responseVacio = await _client.GetAsync(
            "/api/almacen/kardex/stock-actual?fechaDesde=2026-02-01&fechaHasta=2026-02-28");
        var bodyVacio = await responseVacio.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(bodyVacio.GetArrayLength(), Is.EqualTo(0));
    }
}
