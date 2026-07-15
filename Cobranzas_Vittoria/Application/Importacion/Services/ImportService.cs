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

    public ImportService(IEnumerable<IImportProcessor> processors, FileValidator fileValidator)
    {
        ArgumentNullException.ThrowIfNull(processors);
        ArgumentNullException.ThrowIfNull(fileValidator);

        _fileValidator = fileValidator;
        _processorsByModulo = processors
            .ToDictionary(p => p.Modulo, StringComparer.OrdinalIgnoreCase);
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
        _fileValidator.Validar(archivo);

        // 2. Resolucion del processor por modulo.
        if (!_processorsByModulo.TryGetValue(modulo, out var processor))
        {
            var modulosDisponibles = string.Join(", ", _processorsByModulo.Keys.OrderBy(k => k));
            throw new ModuloNoSoportadoException(
                $"El modulo '{modulo}' no es soportado por la API de importacion. " +
                $"Modulos disponibles: {modulosDisponibles}.");
        }

        // 3. Delegacion: el processor encapsula parseo, validacion de estructura,
        //    mapeo, transaccion y traduccion de SqlException -> DatosInvalidosException.
        return await processor.EjecutarAsync(archivo, usuario, ct);
    }
}
