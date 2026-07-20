using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Cobranzas_Vittoria.Controllers;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Controllers;

/// <summary>
/// Pruebas unitarias de <see cref="ImportController"/>.
///
/// El controller es un wrapper delgado sobre <see cref="IImportService"/>:
/// la logica de negocio esta en el service, y el manejo de errores en el
/// <c>ApiExceptionMiddleware</c>. Por eso los tests verifican:
///
///   1. El controller invoca al service con los parametros correctos
///      (modulo, archivo, usuario).
///   2. Devuelve 200 OK con el <see cref="ResultadoImportacion"/>.
///   3. Las excepciones tipadas del service se propagan SIN ser envueltas
///      (no hay try/catch en el action): el middleware las traduce a HTTP.
///
/// Los tests son a nivel de action method, no de HTTP pipeline. La traduccion
/// de excepcion -> HTTP code se cubre con <c>ApiExceptionMiddlewareTests</c> y
/// los tests de integracion end-to-end con Testcontainers (Fase 6).
/// </summary>
public class ImportControllerUnitTests
{
    private readonly RecordingImportService _service;
    private readonly ImportController _controller;

    public ImportControllerUnitTests()
    {
        _service = new RecordingImportService();
        _controller = new ImportController(_service, NullLogger<ImportController>.Instance);
    }

    // =========================================================================
    // Happy path
    // =========================================================================

    [Test]
    public async Task Importar_ModuloValido_Retorna200OkConResultado()
    {
        // Arrange
        var archivo = TestFormFiles.FromText("Codigo,Nombre\nUM-001,Kg\n", "test.csv");
        _service.ResultadoARetornar = new ResultadoImportacion("unidad-medida", "csv", FilasInsertadas: 3);

        // Act
        var actionResult = await _controller.Importar("unidad-medida", archivo, "u1", CancellationToken.None);

        // Assert: tipo y status code
        var ok = actionResult as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.StatusCode, Is.EqualTo(200));

        // Assert: body
        var resultado = (ResultadoImportacion)ok.Value!;
        Assert.That(resultado.Modulo, Is.EqualTo("unidad-medida"));
        Assert.That(resultado.Formato, Is.EqualTo("csv"));
        Assert.That(resultado.FilasInsertadas, Is.EqualTo(3));

        // Assert: delega al service con los parametros correctos
        Assert.That(_service.Llamadas, Is.EqualTo(1));
        Assert.That(_service.UltimoModulo, Is.EqualTo("unidad-medida"));
        Assert.That(_service.UltimoArchivo, Is.SameAs(archivo));
        Assert.That(_service.UltimoUsuario, Is.EqualTo("u1"));
    }

    [Test]
    public async Task Importar_PropagaCancellationTokenAlService()
    {
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");
        _service.ResultadoARetornar = new ResultadoImportacion("unidad-medida", "csv", 0);

        using var cts = new CancellationTokenSource();
        await _controller.Importar("unidad-medida", archivo, "u1", cts.Token);

        Assert.That(_service.UltimoCancellationToken, Is.EqualTo(cts.Token));
    }

    // =========================================================================
    // Propagacion de excepciones del service
    // =========================================================================

    [Test]
    public void Importar_ServiceLanzaArchivoInvalidoException_PropagaExcepcion()
    {
        // El ApiExceptionMiddleware traduce esto a 400 (o 413 si codigo = TAMANIO_EXCEDIDO).
        var archivo = TestFormFiles.FromText("contenido", "datos.txt", "text/plain");
        _service.ExcepcionALanzar = new ArchivoInvalidoException("EXTENSION_INVALIDA", "Extension no permitida.");

        Assert.ThrowsAsync<ArchivoInvalidoException>(async () =>
            await _controller.Importar("unidad-medida", archivo, "u1", CancellationToken.None));
    }

    [Test]
    public void Importar_ServiceLanzaEstructuraInvalidaException_PropagaExcepcion()
    {
        // El ApiExceptionMiddleware traduce esto a 400.
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");
        _service.ExcepcionALanzar = new EstructuraInvalidaException("ENCABEZADOS_INCORRECTOS", "Falta la columna Nombre.");

        Assert.ThrowsAsync<EstructuraInvalidaException>(async () =>
            await _controller.Importar("unidad-medida", archivo, "u1", CancellationToken.None));
    }

    [Test]
    public void Importar_ServiceLanzaDatosInvalidosException_PropagaExcepcion()
    {
        // El ApiExceptionMiddleware traduce esto a 422 con la lista de errores por fila.
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");
        var errores = new[]
        {
            new DetalleErrorFila(2, "Codigo", "CAMPO_REQUERIDO", "Codigo es requerido."),
            new DetalleErrorFila(3, "Nombre", "FORMATO_INVALIDO", "Formato invalido.")
        };
        _service.ExcepcionALanzar = new DatosInvalidosException("2 filas con errores", errores);

        Assert.ThrowsAsync<DatosInvalidosException>(async () =>
            await _controller.Importar("unidad-medida", archivo, "u1", CancellationToken.None));
    }

    [Test]
    public void Importar_ServiceLanzaModuloNoSoportadoException_PropagaExcepcion()
    {
        // El ApiExceptionMiddleware traduce esto a 400 con codigo MODULO_NO_SOPORTADO.
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");
        _service.ExcepcionALanzar = new ModuloNoSoportadoException("El modulo 'foo' no es soportado.");

        Assert.ThrowsAsync<ModuloNoSoportadoException>(async () =>
            await _controller.Importar("foo", archivo, "u1", CancellationToken.None));
    }

    [Test]
    public void Importar_ServiceLanzaExcepcionGenerica_PropagaExcepcion()
    {
        // El ApiExceptionMiddleware traduce esto a 500 UNHANDLED_ERROR.
        var archivo = TestFormFiles.FromText("Codigo,Nombre\n", "test.csv");
        _service.ExcepcionALanzar = new InvalidOperationException("Fallo inesperado.");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _controller.Importar("unidad-medida", archivo, "u1", CancellationToken.None));
    }

    // =========================================================================
    // Validaciones de constructor
    // =========================================================================

    [Test]
    public void Constructor_ServiceNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportController(null!, NullLogger<ImportController>.Instance));
    }

    // =========================================================================
    // Fake de IImportService
    //
    // Permite configurar el resultado o excepcion a devolver sin ejercitar
    // la logica real (parseo, mapeo, SP). El service real se prueba en
    // ImportServiceUnitTests; aca solo se verifica la capa HTTP.
    // =========================================================================

    private sealed class RecordingImportService : IImportService
    {
        public int Llamadas { get; private set; }
        public string? UltimoModulo { get; private set; }
        public IFormFile? UltimoArchivo { get; private set; }
        public string? UltimoUsuario { get; private set; }
        public CancellationToken UltimoCancellationToken { get; private set; }

        public ResultadoImportacion? ResultadoARetornar { get; set; }
        public Exception? ExcepcionALanzar { get; set; }

        public Task<ResultadoImportacion> ImportarAsync(
            string modulo, IFormFile archivo, string usuario, CancellationToken ct = default)
        {
            Llamadas++;
            UltimoModulo = modulo;
            UltimoArchivo = archivo;
            UltimoUsuario = usuario;
            UltimoCancellationToken = ct;

            if (ExcepcionALanzar is not null)
                throw ExcepcionALanzar;

            return Task.FromResult(ResultadoARetornar
                ?? new ResultadoImportacion(modulo, "csv", 0));
        }
    }
}
