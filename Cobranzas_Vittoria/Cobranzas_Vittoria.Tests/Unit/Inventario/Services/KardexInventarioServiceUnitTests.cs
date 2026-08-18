using Cobranzas_Vittoria.Application.Common;
using Cobranzas_Vittoria.Application.Inventario;
using Cobranzas_Vittoria.Application.Inventario.Dtos;
using Cobranzas_Vittoria.Application.Inventario.Excepciones;
using Cobranzas_Vittoria.Application.Inventario.Services;
using Cobranzas_Vittoria.Application.Inventario.Validators;
using Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Inventario.Services;

/// <summary>
/// Pruebas unitarias de <see cref="KardexInventarioService"/>.
///
/// El service tiene tres responsabilidades que validar:
///   1) Invocar al validator antes de cualquier operacion de escritura.
///   2) Delegar al repository correspondiente (Listar/Registrar/Actualizar/Eliminar).
///   3) Capturar SqlException del rango 51100-51199 y traducirla:
///      - 51104 KARDEX_NO_ENCONTRADO -> KardexNoEncontradoException (404).
///      - Otros 51100-51199          -> ValidacionNegocioInventarioException (422).
///   4) Relanzar SqlException fuera del rango (queda como 500 via middleware).
///
/// Los tests usan stubs manuales (no Moq) siguiendo la convencion del
/// proyecto (ver <c>ImportServiceUnitTests</c>).
/// </summary>
public class KardexInventarioServiceUnitTests
{
    // =========================================================================
    // Helpers de construccion
    // =========================================================================

    private static (KardexInventarioService service,
                    StubKardexEntradaRepository entradaRepo,
                    StubKardexSalidaRepository salidaRepo,
                    StubKardexStockRepository stockRepo,
                    KardexInventarioValidator validator)
        CrearServicioConStubs()
    {
        var entradaRepo = new StubKardexEntradaRepository();
        var salidaRepo = new StubKardexSalidaRepository();
        var stockRepo = new StubKardexStockRepository();

        // El validator necesita los repos legacy. Como el service delega
        // TODA la validacion al validator, podemos usar un validator "OK"
        // (sin errores) o uno que falle segun el test. En estos tests
        // asumimos entradas validas y dejamos que el validator nunca lance.
        var esp = new StubEspecialidadRepository();
        esp.Add(1, "Alba");
        var mat = new StubMaterialRepository();
        mat.Add(1, 1, "M1");
        var prov = new StubProveedorRepository();
        prov.Add(1, "P1");
        var proy = new StubProyectoRepository();
        proy.Add(1, "Pry1");
        var validator = new KardexInventarioValidator(esp, mat, prov, proy);

        var service = new KardexInventarioService(
            entradaRepo, salidaRepo, stockRepo, validator,
            NullLogger<KardexInventarioService>.Instance);
        return (service, entradaRepo, salidaRepo, stockRepo, validator);
    }

    private static KardexEntradaCreateDto EntradaValida()
        => new()
        {
            IdEspecialidad = 1,
            IdMaterial = 1,
            IdProveedor = 1,
            IdProyecto = 1,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            Cantidad = 10m
        };

    private static KardexSalidaCreateDto SalidaValida()
        => new()
        {
            IdEspecialidad = 1,
            IdProyecto = 1,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            Solicitante = "Juan",
            Items = new List<KardexSalidaItemCreateDto>
            {
                new() { IdMaterial = 1, Cantidad = 5m }
            }
        };

    // =========================================================================
    // Listar
    // =========================================================================

    [Test]
    public async Task ListarEntradas_DelegaAlRepositorioConFiltroNormalizado()
    {
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnListar = filtro => new List<KardexEntradaResponseDto>
        {
            new() { IdKardexEntrada = 1, IdMaterial = 10, Cantidad = 5m }
        };

        var result = await service.ListarEntradasAsync(
            new KardexFiltroInventarioDto { IdEspecialidad = 1, IdProyecto = 2 });

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(entradaRepo.LlamadasListar, Has.Count.EqualTo(1));
        Assert.That(entradaRepo.LlamadasListar[0].IdEspecialidad, Is.EqualTo(1));
    }

    [Test]
    public async Task ListarEntradas_FiltroNull_LoNormalizaAFiltroVacio()
    {
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnListar = _ => new List<KardexEntradaResponseDto>();

        var result = await service.ListarEntradasAsync(null!);

        Assert.That(result, Is.Empty);
        Assert.That(entradaRepo.LlamadasListar, Has.Count.EqualTo(1));
        Assert.That(entradaRepo.LlamadasListar[0], Is.Not.Null);
    }

    [Test]
    public async Task ListarSalidas_DelegaAlRepositorio()
    {
        var (service, _, salidaRepo, _, _) = CrearServicioConStubs();
        salidaRepo.OnListar = _ => new List<KardexSalidaResponseDto>
        {
            new() { IdKardexSalida = 1, IdMaterial = 10, Cantidad = 3m }
        };

        var result = await service.ListarSalidasAsync(new KardexFiltroInventarioDto());

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(salidaRepo.LlamadasListar, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ListarStockActual_DelegaAlRepositorioConFiltro()
    {
        var (service, _, _, stockRepo, _) = CrearServicioConStubs();
        stockRepo.OnListar = filtro => new List<KardexStockActualResponseDto>
        {
            new() { IdMaterial = 10, Stock = 7m }
        };

        var result = await service.ListarStockActualAsync(new KardexStockFiltroInventarioDto
        {
            IdEspecialidad = 1,
            FechaDesde = new DateOnly(2026, 1, 1),
            FechaHasta = new DateOnly(2026, 1, 31)
        });

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(stockRepo.LlamadasListar, Has.Count.EqualTo(1));
        Assert.That(stockRepo.LlamadasListar[0].IdEspecialidad, Is.EqualTo(1));
        Assert.That(stockRepo.LlamadasListar[0].FechaDesde, Is.EqualTo(new DateOnly(2026, 1, 1)));
    }

    // =========================================================================
    // Registrar
    // =========================================================================

    [Test]
    public async Task RegistrarEntrada_DtoValido_DelegaAlRepositorio()
    {
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnRegistrar = dto => new KardexEntradaResponseDto
        {
            IdKardexEntrada = 99,
            IdEspecialidad = dto.IdEspecialidad,
            IdMaterial = dto.IdMaterial,
            Cantidad = dto.Cantidad
        };

        var result = await service.RegistrarEntradaAsync(EntradaValida());

        Assert.That(result.IdKardexEntrada, Is.EqualTo(99));
        Assert.That(entradaRepo.LlamadasRegistrar, Has.Count.EqualTo(1));
    }

    [Test]
    public void RegistrarEntrada_DtoNull_LanzaArgumentNull()
    {
        var (service, _, _, _, _) = CrearServicioConStubs();
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await service.RegistrarEntradaAsync(null!));
    }

    [Test]
    public async Task RegistrarSalida_DtoValido_DelegaAlRepositorio()
    {
        var (service, _, salidaRepo, _, _) = CrearServicioConStubs();
        salidaRepo.OnRegistrar = dto => new List<KardexSalidaResponseDto>
        {
            new() { IdKardexSalida = 50, IdMaterial = 1, Cantidad = 5m }
        };

        var result = await service.RegistrarSalidaAsync(SalidaValida());

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].IdKardexSalida, Is.EqualTo(50));
    }

    // =========================================================================
    // Actualizar (requiere IdKardex*)
    // =========================================================================

    [Test]
    public void ActualizarEntrada_IdKardexEntradaNulo_Lanza422()
    {
        var (service, _, _, _, _) = CrearServicioConStubs();
        var dto = EntradaValida();
        dto.IdKardexEntrada = null;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await service.ActualizarEntradaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "idKardexEntrada"), Is.True);
    }

    [Test]
    public void ActualizarEntrada_IdKardexEntradaCero_Lanza422()
    {
        var (service, _, _, _, _) = CrearServicioConStubs();
        var dto = EntradaValida();
        dto.IdKardexEntrada = 0;

        Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await service.ActualizarEntradaAsync(dto));
    }

    [Test]
    public async Task ActualizarEntrada_DtoValido_DelegaAlRepositorio()
    {
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnActualizar = dto => new KardexEntradaResponseDto
        {
            IdKardexEntrada = dto.IdKardexEntrada!.Value,
            Cantidad = dto.Cantidad
        };

        var dto = EntradaValida();
        dto.IdKardexEntrada = 5;

        var result = await service.ActualizarEntradaAsync(dto);

        Assert.That(result.IdKardexEntrada, Is.EqualTo(5));
        Assert.That(entradaRepo.LlamadasActualizar, Has.Count.EqualTo(1));
    }

    [Test]
    public void ActualizarSalida_IdKardexSalidaNulo_Lanza422()
    {
        var (service, _, _, _, _) = CrearServicioConStubs();
        var dto = SalidaValida();
        dto.IdKardexSalida = null;

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await service.ActualizarSalidaAsync(dto))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "idKardexSalida"), Is.True);
    }

    [Test]
    public async Task ActualizarSalida_DtoValido_DelegaAlRepositorio()
    {
        var (service, _, salidaRepo, _, _) = CrearServicioConStubs();
        salidaRepo.OnActualizar = dto => new List<KardexSalidaResponseDto>
        {
            new() { IdKardexSalida = dto.IdKardexSalida!.Value }
        };

        var dto = SalidaValida();
        dto.IdKardexSalida = 7;

        var result = await service.ActualizarSalidaAsync(dto);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].IdKardexSalida, Is.EqualTo(7));
    }

    // =========================================================================
    // Eliminar (requiere id > 0)
    // =========================================================================

    [Test]
    public void EliminarEntrada_IdCero_Lanza422()
    {
        var (service, _, _, _, _) = CrearServicioConStubs();
        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await service.EliminarEntradaAsync(0))!;

        Assert.That(ex.Errores.Any(e => e.Campo == "idKardexEntrada"), Is.True);
    }

    [Test]
    public async Task EliminarEntrada_IdPositivo_DelegaAlRepositorio()
    {
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnEliminar = _ => Task.CompletedTask;

        await service.EliminarEntradaAsync(42);

        Assert.That(entradaRepo.LlamadasEliminar, Is.EqualTo(new[] { 42 }));
    }

    [Test]
    public void EliminarSalida_IdCero_Lanza422()
    {
        var (service, _, _, _, _) = CrearServicioConStubs();
        Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await service.EliminarSalidaAsync(0));
    }

    [Test]
    public async Task EliminarSalida_IdPositivo_DelegaAlRepositorio()
    {
        var (service, _, salidaRepo, _, _) = CrearServicioConStubs();
        salidaRepo.OnEliminar = _ => Task.CompletedTask;

        await service.EliminarSalidaAsync(77);

        Assert.That(salidaRepo.LlamadasEliminar, Is.EqualTo(new[] { 77 }));
    }

    // =========================================================================
    // SqlException: traduccion del rango 51100-51199
    //
    // SqlException se construye via SqlExceptionBuilder con Number y Message
    // configurables. El service tiene un `catch (SqlException ex) when
    // (SqlExceptionTranslator.Traducir(ex) is { } traduccion)` que captura
    // y traduce a 422 o 404. Los tests verifican:
    //   - 51104 KARDEX_NO_ENCONTRADO -> KardexNoEncontradoException (404).
    //   - 51110 STOCK_INSUFICIENTE   -> ValidacionNegocioInventarioException (422).
    //   - 51111 STOCK_INCONSISTENTE  -> ValidacionNegocioInventarioException (422).
    //   - 51099 (Compras)            -> relanzado como SqlException (queda 500).
    // =========================================================================

    [Test]
    public void RegistrarEntrada_Sql51110StockInsuficiente_Lanza422ConCodigo()
    {
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnRegistrar = _ => throw SqlExceptionBuilder.Crear(
            51110, "STOCK_INSUFICIENTE: El stock actual es 0, se necesitan 5.");

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await service.RegistrarEntradaAsync(EntradaValida()))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("STOCK_INSUFICIENTE"));
        Assert.That(ex.Errores[0].Mensaje, Does.Contain("stock actual es 0"));
    }

    [Test]
    public void EliminarEntrada_Sql51111StockInconsistenteAlEliminar_Lanza422()
    {
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnEliminar = _ => throw SqlExceptionBuilder.Crear(
            51111, "STOCK_INCONSISTENTE_AL_ELIMINAR: Hay salidas posteriores que dependen de esta entrada.");

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await service.EliminarEntradaAsync(5))!;

        Assert.That(ex.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("STOCK_INCONSISTENTE_AL_ELIMINAR"));
    }

    [Test]
    public void EliminarEntrada_Sql51104KardexNoEncontrado_Lanza404()
    {
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnEliminar = _ => throw SqlExceptionBuilder.Crear(
            51104, "KARDEX_NO_ENCONTRADO: No existe el idKardexEntrada=999.");

        var ex = Assert.ThrowsAsync<KardexNoEncontradoException>(async () =>
            await service.EliminarEntradaAsync(999))!;

        Assert.That(ex.TipoKardex, Is.EqualTo("entrada"));
    }

    [Test]
    public void EliminarSalida_Sql51104KardexNoEncontrado_Lanza404TipoSalida()
    {
        var (service, _, salidaRepo, _, _) = CrearServicioConStubs();
        salidaRepo.OnEliminar = _ => throw SqlExceptionBuilder.Crear(
            51104, "KARDEX_NO_ENCONTRADO: No existe el idKardexSalida=999.");

        var ex = Assert.ThrowsAsync<KardexNoEncontradoException>(async () =>
            await service.EliminarSalidaAsync(999))!;

        Assert.That(ex.TipoKardex, Is.EqualTo("salida"));
    }

    [Test]
    public void RegistrarEntrada_SqlFueraDeRango_SeRelanza_NoSeTraduce()
    {
        // Errores fuera de 51100-51199 (ej: 500xx, 51099 del modulo Compras)
        // se relanzan crudos: el middleware los mapea a 500 SQL_ERROR.
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnRegistrar = _ => throw SqlExceptionBuilder.Crear(
            51099, "COMPRAS: error del modulo Compras.");

        Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(async () =>
            await service.RegistrarEntradaAsync(EntradaValida()));
    }

    [Test]
    public void RegistrarEntrada_SqlGenericoSinCodigoPrefijo_Lanza422ConCodigoGenerico()
    {
        // Sin separador ':' en el mensaje, el translator debe usar
        // un codigo generico derivado (ERROR_VALIDACION) y envolver
        // el mensaje completo como detalle para mostrar al cliente.
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnRegistrar = _ => throw SqlExceptionBuilder.Crear(
            51110, "Error generico del SP sin prefijo CODIGO");

        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(async () =>
            await service.RegistrarEntradaAsync(EntradaValida()))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("ERROR_VALIDACION"));
    }

    [Test]
    public void SqlExceptionTranslator_51110_DevuelveCodigoStockInsuficiente_Regresion()
    {
        // Regresion: la logica que el service usa para traducir 51110 -> STOCK_INSUFICIENTE
        // es la de SqlExceptionTranslator. Si el formato del SP cambia,
        // este test lo detecta antes que el service.
        var result = SqlExceptionTranslator.Traducir(51110, "STOCK_INSUFICIENTE: Stock actual = 0, solicitado = 5.");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodigoError, Is.EqualTo("STOCK_INSUFICIENTE"));
        Assert.That(result.Mensaje, Does.Contain("Stock actual = 0"));
        Assert.That(result.NumeroSql, Is.EqualTo(51110));
    }

    [Test]
    public void SqlExceptionTranslator_51104_DevuelveCodigoKardexNoEncontrado_Regresion()
    {
        var result = SqlExceptionTranslator.Traducir(51104, "KARDEX_NO_ENCONTRADO: No existe el idKardexSalida=999.");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodigoError, Is.EqualTo("KARDEX_NO_ENCONTRADO"));
    }

    [Test]
    public void SqlExceptionTranslator_51099Compras_DevuelveNull_Regresion()
    {
        // Los errores del modulo Compras (50xxx-51099) NO deben traducirse
        // desde Inventario: se relanzan como 500 SQL_ERROR por el middleware.
        var result = SqlExceptionTranslator.Traducir(51099, "COMPRAS: detalle.");
        Assert.That(result, Is.Null);
    }

    // =========================================================================
    // Constructor: argumentos requeridos
    // =========================================================================

    [Test]
    public void Constructor_RepositorioNulo_LanzaArgumentNull()
    {
        var entradaRepo = new StubKardexEntradaRepository();
        var salidaRepo = new StubKardexSalidaRepository();
        var stockRepo = new StubKardexStockRepository();
        var esp = new StubEspecialidadRepository();
        var mat = new StubMaterialRepository();
        var prov = new StubProveedorRepository();
        var proy = new StubProyectoRepository();
        var validator = new KardexInventarioValidator(esp, mat, prov, proy);

        Assert.Throws<ArgumentNullException>(() => new KardexInventarioService(
            null!, salidaRepo, stockRepo, validator,
            NullLogger<KardexInventarioService>.Instance));

        Assert.Throws<ArgumentNullException>(() => new KardexInventarioService(
            entradaRepo, null!, stockRepo, validator,
            NullLogger<KardexInventarioService>.Instance));

        Assert.Throws<ArgumentNullException>(() => new KardexInventarioService(
            entradaRepo, salidaRepo, null!, validator,
            NullLogger<KardexInventarioService>.Instance));

        Assert.Throws<ArgumentNullException>(() => new KardexInventarioService(
            entradaRepo, salidaRepo, stockRepo, null!,
            NullLogger<KardexInventarioService>.Instance));
    }
}
