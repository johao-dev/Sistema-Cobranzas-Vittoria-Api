using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Application.Excepciones;
using Cobranzas_Vittoria.Inventario.Domain.Excepciones;
using Cobranzas_Vittoria.Inventario.Application.Kardex;
using Cobranzas_Vittoria.Inventario.Application.Common;
using Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Inventario.Application;

/// <summary>
/// Pruebas unitarias de <see cref="KardexInventarioService"/>.
///
/// El servicio valida, delega al repositorio y propaga las excepciones de negocio.
/// La traducción SQL se verifica en las pruebas de infraestructura del módulo.
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
            new StubExcelExporter(),
            NullLogger<KardexInventarioService>.Instance);
        return (service, entradaRepo, salidaRepo, stockRepo, validator);
    }

    private static RegistroKardexEntrada EntradaValida()
        => new()
        {
            IdMaterial = 1,
            IdProveedor = 1,
            IdProyecto = 1,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            Cantidad = 10m
        };

    private static RegistroKardexSalida SalidaValida()
        => new()
        {
            IdProyecto = 1,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            Solicitante = "Juan",
            Items = new List<ItemKardexSalida>
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
        entradaRepo.OnListar = filtro => new List<KardexEntrada>
        {
            new() { IdKardexEntrada = 1, IdMaterial = 10, Cantidad = 5m }
        };

        var result = await service.ListarEntradasAsync(
            new FiltroKardex { IdEspecialidad = 1, IdProyecto = 2 });

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(entradaRepo.LlamadasListar, Has.Count.EqualTo(1));
        Assert.That(entradaRepo.LlamadasListar[0].IdEspecialidad, Is.EqualTo(1));
    }

    [Test]
    public async Task ListarEntradas_FiltroNull_LoNormalizaAFiltroVacio()
    {
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        entradaRepo.OnListar = _ => new List<KardexEntrada>();

        var result = await service.ListarEntradasAsync(null!);

        Assert.That(result, Is.Empty);
        Assert.That(entradaRepo.LlamadasListar, Has.Count.EqualTo(1));
        Assert.That(entradaRepo.LlamadasListar[0], Is.Not.Null);
    }

    [Test]
    public async Task ListarSalidas_DelegaAlRepositorio()
    {
        var (service, _, salidaRepo, _, _) = CrearServicioConStubs();
        salidaRepo.OnListar = _ => new List<KardexSalida>
        {
            new() { IdKardexSalida = 1, IdMaterial = 10, Cantidad = 3m }
        };

        var result = await service.ListarSalidasAsync(new FiltroKardex());

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(salidaRepo.LlamadasListar, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ListarStockActual_DelegaAlRepositorioConFiltro()
    {
        var (service, _, _, stockRepo, _) = CrearServicioConStubs();
        stockRepo.OnListar = filtro => new List<StockKardex>
        {
            new() { IdMaterial = 10, Stock = 7m }
        };

        var result = await service.ListarStockActualAsync(new FiltroStockKardex
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
        entradaRepo.OnRegistrar = dto => new KardexEntrada
        {
            IdKardexEntrada = 99,
            IdEspecialidad = 1,
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
        salidaRepo.OnRegistrar = dto => new List<KardexSalida>
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
        entradaRepo.OnActualizar = dto => new KardexEntrada
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
        salidaRepo.OnActualizar = dto => new List<KardexSalida>
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

    [Test]
    public void RegistrarEntrada_ErrorDeNegocioDelRepositorio_PropagaLaMismaExcepcion()
    {
        var (service, entradaRepo, _, _, _) = CrearServicioConStubs();
        KardexNoEncontradoException error = new("entrada", 999);
        entradaRepo.OnRegistrar = _ => throw error;

        KardexNoEncontradoException? resultado = Assert.ThrowsAsync<KardexNoEncontradoException>(
            () => service.RegistrarEntradaAsync(EntradaValida()));

        Assert.That(resultado, Is.SameAs(error));
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
        var exporter = new StubExcelExporter();

        Assert.Throws<ArgumentNullException>(() => new KardexInventarioService(
            null!, salidaRepo, stockRepo, validator, exporter,
            NullLogger<KardexInventarioService>.Instance));

        Assert.Throws<ArgumentNullException>(() => new KardexInventarioService(
            entradaRepo, null!, stockRepo, validator, exporter,
            NullLogger<KardexInventarioService>.Instance));

        Assert.Throws<ArgumentNullException>(() => new KardexInventarioService(
            entradaRepo, salidaRepo, null!, validator, exporter,
            NullLogger<KardexInventarioService>.Instance));

        Assert.Throws<ArgumentNullException>(() => new KardexInventarioService(
            entradaRepo, salidaRepo, stockRepo, null!, exporter,
            NullLogger<KardexInventarioService>.Instance));

        Assert.Throws<ArgumentNullException>(() => new KardexInventarioService(
            entradaRepo, salidaRepo, stockRepo, validator, null!,
            NullLogger<KardexInventarioService>.Instance));
    }

    // =========================================================================
    // ExportarStockActualAsync
    // =========================================================================

    [Test]
    public async Task ExportarStockActual_ConDatos_LlamaAlRepositoryYDelegaAlExporter()
    {
        // Arrange
        var exporter = new StubExcelExporter();
        var serviceConExporter = CrearServiceConExporter(exporter);
        var stockRepo = serviceConExporter.StockRepo;

        stockRepo.OnListar = _ => new List<StockKardex>
        {
            new()
            {
                IdKardexStock = 1,
                IdMaterial = 2,
                CodigoMaterial = "MAT-0001",
                Nombre = "MORTERO LISTO",
                UnidadMedida = "BOL",
                IdEspecialidad = 2,
                Especialidad = "Albañilería",
                TotalEntrada = 50m,
                TotalSalida = 3m,
                Stock = 47m,
                FechaUltimaMovimiento = new DateOnly(2026, 1, 16)
            }
        };

        var filtro = new FiltroStockKardex
        {
            IdEspecialidad = 2
        };

        // Act
        var bytes = await serviceConExporter.Service.ExportarStockActualAsync(filtro);

        // Assert
        Assert.That(bytes, Is.Not.Null, "El servicio debe devolver los bytes del exporter (pueden ser vacios del stub).");
        Assert.That(exporter.Llamadas, Is.EqualTo(1), "Debe invocarse el exporter exactamente una vez.");
        Assert.That(stockRepo.LlamadasListar, Has.Count.EqualTo(1), "Debe consultarse el stock repo una vez.");
        Assert.That(stockRepo.LlamadasListar[0].IdEspecialidad, Is.EqualTo(2));
    }

    [Test]
    public async Task ExportarStockActual_FiltroNull_SeTrataComoVacio()
    {
        // Arrange
        var exporter = new StubExcelExporter();
        var serviceConExporter = CrearServiceConExporter(exporter);
        var (stockRepo, _, _, _, _) = serviceConExporter.GetStubs();

        // Act
        await serviceConExporter.Service.ExportarStockActualAsync(null);

        // Assert
        Assert.That(stockRepo.LlamadasListar, Has.Count.EqualTo(1));
        Assert.That(stockRepo.LlamadasListar[0].IdEspecialidad, Is.Null);
    }

    [Test]
    public async Task ExportarStockActual_SinDatos_GeneraArchivoVacioYDelegaAlExporter()
    {
        // Arrange
        var exporter = new StubExcelExporter();
        var serviceConExporter = CrearServiceConExporter(exporter);
        var (_, _, _, _, _) = serviceConExporter.GetStubs();

        // Act
        await serviceConExporter.Service.ExportarStockActualAsync(new FiltroStockKardex());

        // Assert
        Assert.That(exporter.Llamadas, Is.EqualTo(1),
            "El exporter debe invocarse aunque no haya datos (genera workbook con solo el header).");
    }

    [Test]
    public async Task ExportarStockActual_ConExcepcionDeRepository_PropagaLaExcepcion()
    {
        // Arrange
        var exporter = new StubExcelExporter();
        var serviceConExporter = CrearServiceConExporter(exporter);
        var (stockRepo, _, _, _, _) = serviceConExporter.GetStubs();
        stockRepo.OnListar = _ => throw new InvalidOperationException("BD no disponible");

        // Act + Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => serviceConExporter.Service.ExportarStockActualAsync(new FiltroStockKardex()));
        Assert.That(ex!.Message, Is.EqualTo("BD no disponible"));
        Assert.That(exporter.Llamadas, Is.EqualTo(0),
            "El exporter no debe invocarse si la consulta al repo fallo.");
    }

    [Test]
    public async Task ExportarStockActual_ErrorDeNegocioDelRepositorio_PropagaSinExportar()
    {
        // Arrange
        var exporter = new StubExcelExporter();
        var serviceConExporter = CrearServiceConExporter(exporter);
        var stockRepo = serviceConExporter.StockRepo;
        ValidacionNegocioInventarioException error = new(new[]
        {
            new DetalleErrorValidacion(null, string.Empty, CodigosErrorInventario.Stock.StockInsuficiente,
                "idMaterial=2 disponible=2.00 solicitado=10.00")
        });
        stockRepo.OnListar = _ => throw error;

        // Act + Assert
        var ex = Assert.ThrowsAsync<ValidacionNegocioInventarioException>(
            () => serviceConExporter.Service.ExportarStockActualAsync(new FiltroStockKardex()));
        Assert.That(ex, Is.SameAs(error));
        Assert.That(ex!.Errores, Has.Count.EqualTo(1));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo(CodigosErrorInventario.Stock.StockInsuficiente));
        Assert.That(exporter.Llamadas, Is.EqualTo(0),
            "El exporter no debe invocarse si la consulta fallo con error de validacion.");
    }

    // =========================================================================
    // Helpers para ExportarStockActualAsync
    // =========================================================================

    /// <summary>
    /// Crea el service usando un <see cref="StubExcelExporter"/> provisto
    /// (no el por defecto) para poder inspeccionar las llamadas.
    /// </summary>
    private static ServiceConStubs CrearServiceConExporter(StubExcelExporter exporter)
    {
        var entradaRepo = new StubKardexEntradaRepository();
        var salidaRepo = new StubKardexSalidaRepository();
        var stockRepo = new StubKardexStockRepository();
        var esp = new StubEspecialidadRepository();
        var mat = new StubMaterialRepository();
        var prov = new StubProveedorRepository();
        var proy = new StubProyectoRepository();
        var validator = new KardexInventarioValidator(esp, mat, prov, proy);

        var service = new KardexInventarioService(
            entradaRepo, salidaRepo, stockRepo, validator,
            exporter,
            NullLogger<KardexInventarioService>.Instance);
        return new ServiceConStubs(service, entradaRepo, salidaRepo, stockRepo, validator);
    }

    private sealed record ServiceConStubs(
        KardexInventarioService Service,
        StubKardexEntradaRepository EntradaRepo,
        StubKardexSalidaRepository SalidaRepo,
        StubKardexStockRepository StockRepo,
        KardexInventarioValidator Validator)
    {
        public (StubKardexStockRepository, StubKardexEntradaRepository, StubKardexSalidaRepository, StubKardexStockRepository, KardexInventarioValidator) GetStubs()
            => (StockRepo, EntradaRepo, SalidaRepo, StockRepo, Validator);
    }
}
