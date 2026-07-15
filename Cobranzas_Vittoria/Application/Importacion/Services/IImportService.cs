using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Microsoft.AspNetCore.Http;

namespace Cobranzas_Vittoria.Application.Importacion.Services;

/// <summary>
/// Fachada de la feature de importacion masiva.
///
/// Es el unico punto de entrada que consume el controller. Su responsabilidad
/// es triple:
///   1. Validar el archivo (extension, MIME, tamano) usando
///      <see cref="Validators.FileValidator"/>. Esto se hace ANTES de elegir
///      processor: si el archivo no es valido, no importa el modulo.
///   2. Resolver el <see cref="IImportProcessor"/> adecuado segun
///      <paramref name="modulo"/> (identificador del segmento de URL
///      <c>/api/import/{modulo}</c>). Si no existe, lanza
///      <see cref="ModuloNoSoportadoException"/> (HTTP 400).
///   3. Delegar al processor, que ejecuta el algoritmo comun
///      (parseo, validacion de estructura, mapeo, transaccion, SP).
///
/// El service NO traduce excepciones: el processor ya encapsula la traduccion
/// de <c>SqlException</c> a <see cref="DatosInvalidosException"/>, y las
/// excepciones de archivo/estructura se propagan tal cual al
/// <c>ApiExceptionMiddleware</c> para mantener un unico contrato HTTP.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Ejecuta la importacion del archivo sobre el modulo indicado.
    /// </summary>
    /// <param name="modulo">
    /// Identificador del modulo, case-insensitive (ej: <c>"unidad-medida"</c>,
    /// <c>"especialidad"</c>, <c>"material"</c>, <c>"proveedor"</c>,
    /// <c>"proveedor-gasto"</c>, <c>"proveedor-terreno"</c>, <c>"categoria-gasto"</c>).
    /// </param>
    /// <param name="archivo">Archivo a importar (.csv, .xlsx o .xls).</param>
    /// <param name="usuario">Identificador del usuario que ejecuta la operacion.</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <returns>Resultado con la cantidad de filas insertadas y el formato del archivo.</returns>
    /// <exception cref="ModuloNoSoportadoException">Si el modulo no tiene processor registrado (HTTP 400).</exception>
    /// <exception cref="ArchivoInvalidoException">Si el archivo no cumple extension/MIME/tamano (HTTP 400/413).</exception>
    /// <exception cref="EstructuraInvalidaException">Si la estructura del archivo es invalida (HTTP 400).</exception>
    /// <exception cref="DatosInvalidosException">Si hay errores de validacion por fila o del SP (HTTP 422).</exception>
    Task<ResultadoImportacion> ImportarAsync(
        string modulo,
        IFormFile archivo,
        string usuario,
        CancellationToken ct = default);
}
