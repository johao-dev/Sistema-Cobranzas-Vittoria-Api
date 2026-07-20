using Cobranzas_Vittoria.Application.Importacion;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Cobranzas_Vittoria.Application.Importacion.Validators;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Services;

/// <summary>
/// Pruebas unitarias de <see cref="ImportService"/>.
///
/// El service es muy simple: valida el archivo, resuelve el processor por
/// modulo y delega. Los tests se enfocan en:
///   - Resolucion correcta del processor por modulo (incluyendo case-insensitive).
///   - Lanzamiento de <see cref="ModuloNoSoportadoException"/> ante modulo desconocido.
///   - Validacion de argumentos basicos (modulo/archivo/usuario nulos o vacios).
///   - Delegacion a <see cref="FileValidator"/> (archivo vacio, extension invalida).
///
/// La logica interna del processor (parseo, mapeo, SP) se cubre en sus
/// propios tests; aca solo se verifica que el service la invoca.
/// </summary>
public class ImportServiceUnitTests
{
    private readonly RecordingProcessor _unidadMedidaProcessor;
    private readonly RecordingProcessor _materialProcessor;
    private readonly ImportService _service;

    public ImportServiceUnitTests()
    {
        _unidadMedidaProcessor = new RecordingProcessor("unidad-medida");
        _materialProcessor = new RecordingProcessor("material");
        _service = new ImportService(
            new IImportProcessor[] { _unidadMedidaProcessor, _materialProcessor },
            new FileValidator(NullLogger<FileValidator>.Instance),
            NullLogger<ImportService>.Instance);
    }

    [SetUp]
    public void ResetearEstadoCompartido()
    {
        // NUnit crea UNA instancia del fixture y reutiliza los RecordingProcessor
        // en todos los tests; reseteamos el contador de llamadas para que cada
        // test arranque con Llamadas = 0.
        _unidadMedidaProcessor.Resetear();
        _materialProcessor.Resetear();
    }

    // =========================================================================
    // Resolucion por modulo
    // =========================================================================

    [Test]
    public async Task ImportarAsync_ModuloValido_DelegaAlProcessorCorrecto()
    {
        var archivo = TestFormFiles.FromText("Codigo,Nombre\nUM-001,Kg\n", "test.csv");

        var resultado = await _service.ImportarAsync("unidad-medida", archivo, "usuario-test");

        Assert.That(resultado, Is.Not.Null);
        Assert.That(resultado.Modulo, Is.EqualTo("unidad-medida"));
        Assert.That(_unidadMedidaProcessor.Llamadas, Is.EqualTo(1));
        Assert.That(_materialProcessor.Llamadas, Is.EqualTo(0));
        Assert.That(_unidadMedidaProcessor.UltimoArchivo, Is.SameAs(archivo));
        Assert.That(_unidadMedidaProcessor.UltimoUsuario, Is.EqualTo("usuario-test"));
    }

    [Test]
    public async Task ImportarAsync_ModuloCaseInsensitive_EncuentraProcessor()
    {
        var archivo = TestFormFiles.FromText("Codigo,Nombre\nUM-001,Kg\n", "test.csv");

        // Misma operacion con mayusculas distintas.
        var resultado1 = await _service.ImportarAsync("UNIDAD-MEDIDA", archivo, "u1");
        var resultado2 = await _service.ImportarAsync("Unidad-Medida", archivo, "u2");
        var resultado3 = await _service.ImportarAsync("unidad-medida", archivo, "u3");

        Assert.That(resultado1.Modulo, Is.EqualTo("unidad-medida"));
        Assert.That(resultado2.Modulo, Is.EqualTo("unidad-medida"));
        Assert.That(resultado3.Modulo, Is.EqualTo("unidad-medida"));
        Assert.That(_unidadMedidaProcessor.Llamadas, Is.EqualTo(3));
    }

    [Test]
    public void ImportarAsync_ModuloNoExiste_LanzaModuloNoSoportadoException()
    {
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");

        var ex = Assert.ThrowsAsync<ModuloNoSoportadoException>(async () =>
            await _service.ImportarAsync("modulo-inexistente", archivo, "u1"))!;

        Assert.That(ex.Message, Does.Contain("modulo-inexistente"));
        // El mensaje tambien deberia listar los modulos disponibles para que
        // el cliente sepa que URL puede usar.
        Assert.That(ex.Message, Does.Contain("unidad-medida"));
        Assert.That(ex.Message, Does.Contain("material"));
        Assert.That(_unidadMedidaProcessor.Llamadas, Is.EqualTo(0));
        Assert.That(_materialProcessor.Llamadas, Is.EqualTo(0));
    }

    [Test]
    public void ImportarAsync_ModuloNoExiste_ArchivoValido_LanzaModuloNoSoportadoException()
    {
        // El archivo es valido (no vacio) pero el modulo no existe.
        // El service valida el archivo primero y luego resuelve el modulo,
        // asi que este test verifica el segundo paso.
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");

        var ex = Assert.ThrowsAsync<ModuloNoSoportadoException>(async () =>
            await _service.ImportarAsync("modulo-inexistente", archivo, "u1"))!;

        Assert.That(ex.Message, Does.Contain("modulo-inexistente"));
        Assert.That(_unidadMedidaProcessor.Llamadas, Is.EqualTo(0));
        Assert.That(_materialProcessor.Llamadas, Is.EqualTo(0));
    }

    // =========================================================================
    // Validacion de argumentos
    // =========================================================================

    [Test]
    public void ImportarAsync_ModuloNulo_LanzaArgumentNullException()
    {
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");

        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.ImportarAsync(null!, archivo, "u1"));
    }

    [Test]
    public void ImportarAsync_ModuloVacio_LanzaArgumentException()
    {
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.ImportarAsync("", archivo, "u1"));
    }

    [Test]
    public void ImportarAsync_ArchivoNulo_LanzaArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.ImportarAsync("unidad-medida", null!, "u1"));
    }

    [Test]
    public void ImportarAsync_UsuarioVacio_LanzaArgumentException()
    {
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.ImportarAsync("unidad-medida", archivo, ""));
    }

    // =========================================================================
    // Delegacion a FileValidator
    // =========================================================================

    [Test]
    public void ImportarAsync_ArchivoVacio_LanzaArchivoInvalidoException_NoDelega()
    {
        // Un CSV de 0 bytes: el FileValidator debe rechazarlo.
        var archivoVacio = TestFormFiles.FromBytes(Array.Empty<byte>(), "vacio.csv", "text/csv");

        var ex = Assert.ThrowsAsync<ArchivoInvalidoException>(async () =>
            await _service.ImportarAsync("unidad-medida", archivoVacio, "u1"))!;

        Assert.That(ex.Codigo, Is.EqualTo("ARCHIVO_VACIO"));
        Assert.That(_unidadMedidaProcessor.Llamadas, Is.EqualTo(0));
    }

    [Test]
    public void ImportarAsync_ExtensionInvalida_LanzaArchivoInvalidoException_NoDelega()
    {
        var archivo = TestFormFiles.FromText("contenido", "datos.txt", "text/plain");

        var ex = Assert.ThrowsAsync<ArchivoInvalidoException>(async () =>
            await _service.ImportarAsync("unidad-medida", archivo, "u1"))!;

        Assert.That(ex.Codigo, Is.EqualTo("EXTENSION_INVALIDA"));
        Assert.That(_unidadMedidaProcessor.Llamadas, Is.EqualTo(0));
    }

    // =========================================================================
    // Comportamiento sin processors registrados
    // =========================================================================

    [Test]
    public void Constructor_SinProcessors_CualquierModuloLanzaModuloNoSoportado()
    {
        var service = new ImportService(Array.Empty<IImportProcessor>(), new FileValidator(NullLogger<FileValidator>.Instance), NullLogger<ImportService>.Instance);
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");

        var ex = Assert.ThrowsAsync<ModuloNoSoportadoException>(async () =>
            await service.ImportarAsync("unidad-medida", archivo, "u1"))!;

        Assert.That(ex.Message, Does.Contain("unidad-medida"));
    }

    [Test]
    public void Constructor_ProcessorsNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportService(null!, new FileValidator(NullLogger<FileValidator>.Instance), NullLogger<ImportService>.Instance));
    }

    [Test]
    public void Constructor_FileValidatorNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ImportService(new IImportProcessor[] { _unidadMedidaProcessor }, null!, NullLogger<ImportService>.Instance));
    }

    // =========================================================================
    // Fake de IImportProcessor
    //
    // Captura el ultimo archivo y usuario recibidos para verificar la
    // delegacion sin ejercitar la logica real del Template Method.
    // =========================================================================

    private sealed class RecordingProcessor : IImportProcessor
    {
        public RecordingProcessor(string modulo) => Modulo = modulo;

        public string Modulo { get; }
        public int Llamadas { get; private set; }
        public IFormFile? UltimoArchivo { get; private set; }
        public string? UltimoUsuario { get; private set; }
        public int FilasARetornar { get; init; } = 0;

        public void Resetear()
        {
            Llamadas = 0;
            UltimoArchivo = null;
            UltimoUsuario = null;
        }

        public Task<ResultadoImportacion> EjecutarAsync(IFormFile file, string usuario, CancellationToken ct)
        {
            Llamadas++;
            UltimoArchivo = file;
            UltimoUsuario = usuario;
            return Task.FromResult(new ResultadoImportacion(Modulo, "csv", FilasARetornar));
        }
    }
}
