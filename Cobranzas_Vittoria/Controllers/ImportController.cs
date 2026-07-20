using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

/// <summary>
/// Controller HTTP de la feature de importacion masiva.
///
/// Expone un unico endpoint: <c>POST /api/import/{modulo}</c>, donde
/// <c>{modulo}</c> es el identificador del modulo de mantenimiento
/// (ej: <c>unidad-medida</c>, <c>especialidad</c>, <c>material</c>, etc.).
///
/// <para>
/// <b>Request:</b> <c>multipart/form-data</c> con dos campos:
///   - <c>archivo</c>: el archivo (.csv, .xlsx o .xls, max 10 MB).
///   - <c>usuario</c>: identificador del usuario que ejecuta la operacion
///     (se pasa al SP como <c>@Usuario</c> para auditoria).
/// </para>
///
/// <para>
/// <b>Response:</b> 200 OK con <c>{ modulo, formato, filasInsertadas }</c>.
/// El resto de los codigos (400, 413, 422) los emite el
/// <c>ApiExceptionMiddleware</c> cuando el service o el processor lanzan
/// las excepciones tipadas (<see cref="Application.Importacion.Excepciones.ArchivoInvalidoException"/>,
/// <see cref="Application.Importacion.Excepciones.EstructuraInvalidaException"/>,
/// <see cref="Application.Importacion.Excepciones.DatosInvalidosException"/>,
/// <see cref="Application.Importacion.Excepciones.ModuloNoSoportadoException"/>).
/// </para>
///
/// <para>
/// <b>Tamanio maximo:</b> <see cref="RequestSizeLimitAttribute"/> aplica 10 MB + 1 MB
/// de headroom como defense-in-depth sobre la validacion de
/// <c>FileValidator.MaximoTamanioBytes</c>. Si ASP.NET Core recibe un body
/// mas grande, devuelve 413 antes de que el action se ejecute.
/// </para>
/// </summary>
[ApiController]
[Route("api/import")]
public class ImportController : ControllerBase
{
    /// <summary>Tamanio maximo del body (10 MB archivo + 1 MB overhead multipart).</summary>
    private const long MaxRequestSize = 11_000_000;

    private readonly IImportService _service;
    private readonly ILogger<ImportController> _logger;

    public ImportController(IImportService service, ILogger<ImportController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Importa un archivo al modulo indicado.
    /// </summary>
    /// <param name="modulo">Identificador del modulo (case-insensitive).</param>
    /// <param name="archivo">Archivo a importar (.csv, .xlsx o .xls).</param>
    /// <param name="usuario">Identificador del usuario (para @Usuario del SP).</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <response code="200">Carga exitosa. Devuelve { modulo, formato, filasInsertadas }.</response>
    /// <response code="400">Solicitud invalida: modulo no soportado, extension/MIME invalido, encoding invalido, o encabezados faltantes.</response>
    /// <response code="413">El archivo excede 10 MB.</response>
    /// <response code="422">Una o mas filas fallaron la validacion o el SP rechazo los datos. Ninguna fila se inserta (rollback).</response>
    [HttpPost("{modulo}")]
    [RequestSizeLimit(MaxRequestSize)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ResultadoImportacion), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Importar(
        [FromRoute] string modulo,
        // IFormFile NO lleva [FromForm]: Swashbuckle no soporta la combinacion
        // [FromForm] + IFormFile (lanza "Error reading parameter(s) for action
        // ... as [FromForm] attribute used with IFormFile"). ASP.NET Core
        // bindea automaticamente los IFormFile desde el body multipart, asi
        // que omitir el atributo no cambia el comportamiento en runtime.
        IFormFile archivo,
        [FromForm] string usuario,
        CancellationToken ct)
    {
        // El service valida argumentos (modulo no vacio, archivo no nulo, usuario no vacio),
        // valida el archivo (extension/MIME/tamano) y resuelve el processor por modulo.
        // Cualquier excepcion tipada se propaga al ApiExceptionMiddleware que la mapea
        // al HTTP code correspondiente.

        var fileName = archivo?.FileName ?? "(null)";
        var fileSize = archivo?.Length ?? 0;

        // Information: trazabilidad del request. Se suprime en produccion via
        // appsettings.json (LogLevel: Warning para Cobranzas_Vittoria.Controllers.Import).
        _logger.LogInformation(
            "POST /api/import/{Modulo} recibido. Archivo={FileName} Tamano={FileSize}B Usuario={Usuario}",
            modulo, fileName, fileSize, usuario);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var resultado = await _service.ImportarAsync(modulo, archivo, usuario, ct);
            sw.Stop();

            _logger.LogInformation(
                "POST /api/import/{Modulo} OK. Formato={Formato} FilasInsertadas={Filas} Duracion={Duracion}ms Usuario={Usuario}",
                modulo, resultado.Formato, resultado.FilasInsertadas, sw.ElapsedMilliseconds, usuario);

            return Ok(resultado);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            // El cliente se desconecto. Es un evento esperado, no un error,
            // pero conviene dejarlo en Warning para detectarlo en production.
            _logger.LogWarning(
                "POST /api/import/{Modulo} CANCELADO por el cliente despues de {Duracion}ms Usuario={Usuario}",
                modulo, sw.ElapsedMilliseconds, usuario);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            // Log de Error con contexto. La inner exception completa la capturara
            // el ApiExceptionMiddleware (que tambien loguea) pero aqui dejamos
            // el rastro del controller para facilitar la busqueda por modulo.
            _logger.LogError(ex,
                "POST /api/import/{Modulo} FALLO despues de {Duracion}ms Usuario={Usuario} TipoError={TipoError}",
                modulo, sw.ElapsedMilliseconds, usuario, ex.GetType().Name);
            throw;
        }
    }
}
