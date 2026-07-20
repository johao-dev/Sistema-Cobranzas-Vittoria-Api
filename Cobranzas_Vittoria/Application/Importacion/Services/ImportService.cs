using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Application.Importacion.Validators;
using Microsoft.AspNetCore.Http;

namespace Cobranzas_Vittoria.Application.Importacion.Services;

/// <summary>
/// Implementacion por defecto de <see cref="IImportService"/>.
///
/// Construye un diccionario <c>modulo -> processor</c> una sola vez en el
/// constructor a partir de todos los <see cref="IImportProcessor"/> registrados
/// en el container de DI. Esto evita escanear la coleccion en cada request.
///
/// <para>
/// <b>Resolucion de processor:</b> se hace por <see cref="IImportProcessor.Modulo"/>
/// con <see cref="StringComparison.OrdinalIgnoreCase"/>, de modo que
/// <c>/api/import/Unidad-Medida</c> y <c>/api/import/unidad-medida</c>
/// caen en el mismo processor. Si la coleccion tiene dos processors con el
/// mismo modulo (error de configuracion), gana el ultimo.
/// </para>
///
/// <para>
/// <b>Por que se inyecta <see cref="IEnumerable{T}"/> y no keyed services:</b>
/// keyed services (<c>AddKeyedScoped</c>) de .NET 8 obligan al caller a usar
/// <c>IServiceProvider</c> + <c>[FromKeyedServices]</c>, lo cual rompe la
/// composicion del controller. <see cref="IEnumerable{T}"/> es la forma mas
/// simple y testeable: el service los enumera una sola vez en el constructor.
/// </para>
/// </summary>
public sealed class ImportService : IImportService
{
    private readonly IReadOnlyDictionary<string, IImportProcessor> _processorsByModulo;
    private readonly FileValidator _fileValidator;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        IEnumerable<IImportProcessor> processors,
        FileValidator fileValidator,
        ILogger<ImportService> logger)
    {
        ArgumentNullException.ThrowIfNull(processors);
        ArgumentNullException.ThrowIfNull(fileValidator);
        ArgumentNullException.ThrowIfNull(logger);

        _fileValidator = fileValidator;
        _logger = logger;
        _processorsByModulo = processors
            .ToDictionary(p => p.Modulo, StringComparer.OrdinalIgnoreCase);

        // Information: el catalogo de modulos se arma una sola vez por request-scoped
        // service. Loguearlo al construir el diccionario es ruidoso; lo dejamos en
        // Debug para que solo aparezca con log level Verbose o superior.
        _logger.LogDebug(
            "ImportService inicializado con {Cantidad} processors: {Modulos}",
            _processorsByModulo.Count, string.Join(", ", _processorsByModulo.Keys));
    }

    public async Task<ResultadoImportacion> ImportarAsync(
        string modulo,
        IFormFile archivo,
        string usuario,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(modulo);
        ArgumentNullException.ThrowIfNull(archivo);
        ArgumentException.ThrowIfNullOrEmpty(usuario);

        // 1. Validacion del archivo ANTES de resolver el processor: si el archivo
        //    es invalido, no tiene sentido gastar el lookup.
        // _fileValidator.Validar(archivo) lanza ArchivoInvalidoException con codigo
        // (ARCHIVO_VACIO, TAMANIO_EXCEDIDO, EXTENSION_INVALIDA, MIME_INVALIDO);
        // el codigo lo captura el ApiExceptionMiddleware y lo mapea al HTTP code.
        _fileValidator.Validar(archivo);
        _logger.LogDebug(
            "Archivo {FileName} ({Tamano}B) supero la validacion estructural",
            archivo.FileName, archivo.Length);

        // 2. Resolucion del processor por modulo.
        if (!_processorsByModulo.TryGetValue(modulo, out var processor))
        {
            var modulosDisponibles = string.Join(", ", _processorsByModulo.Keys.OrderBy(k => k));
            // Warning: el cliente pidio un modulo que no existe. Vale la pena
            // registrarlo en produccion para detectar typos o intentos maliciosos.
            _logger.LogWarning(
                "Modulo '{Modulo}' no soportado por la API de importacion. Modulos disponibles: {Disponibles}",
                modulo, modulosDisponibles);
            throw new ModuloNoSoportadoException(
                $"El modulo '{modulo}' no es soportado por la API de importacion. " +
                $"Modulos disponibles: {modulosDisponibles}.");
        }

        _logger.LogDebug(
            "Processor resuelto para modulo '{Modulo}': {TipoProcessor}",
            modulo, processor.GetType().Name);

        // 3. Delegacion: el processor encapsula parseo, validacion de estructura,
        //    mapeo, transaccion y traduccion de SqlException -> DatosInvalidosException.
        return await processor.EjecutarAsync(archivo, usuario, ct);
    }
}
