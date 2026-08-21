using System.Text;
using Cobranzas_Vittoria.Application.Common.Exports;
using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

/// <summary>
/// Controller HTTP de la feature de importacion masiva.
///
/// Expone dos endpoints bajo <c>/api/import/{modulo}</c>:
///   - <c>POST /{modulo}</c>: importa un archivo al modulo indicado.
///   - <c>GET  /{modulo}/plantilla</c>: descarga una plantilla (CSV o XLSX)
///     con los encabezados requeridos del modulo, lista para que el usuario
///     la rellene y la suba por el endpoint POST.
/// </summary>
[ApiController]
[Route("api/import")]
public class ImportController : ControllerBase
{
    /// <summary>Tamanio maximo del body (10 MB archivo + 1 MB overhead multipart).</summary>
    private const long MaxRequestSize = 11_000_000;

    private static readonly string[] EncabezadosMaterial = { "Especialidad", "Nombre", "UnidadMedida", "Codigo" };

    private readonly IImportService _service;
    private readonly IExcelExporter _excelExporter;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        IImportService service,
        IExcelExporter excelExporter,
        ILogger<ImportController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _excelExporter = excelExporter ?? throw new ArgumentNullException(nameof(excelExporter));
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
            _logger.LogWarning(
                "POST /api/import/{Modulo} CANCELADO por el cliente despues de {Duracion}ms Usuario={Usuario}",
                modulo, sw.ElapsedMilliseconds, usuario);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "POST /api/import/{Modulo} FALLO despues de {Duracion}ms Usuario={Usuario} TipoError={TipoError}",
                modulo, sw.ElapsedMilliseconds, usuario, ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Descarga una plantilla (CSV o XLSX) con los encabezados requeridos
    /// por el modulo indicado, lista para que el operador la rellene y la
    /// suba por <c>POST /api/import/{modulo}</c>.
    /// </summary>
    /// <param name="modulo">Identificador del modulo (case-insensitive). Por
    /// ahora solo se soporta <c>material</c>.</param>
    /// <param name="formato">Formato de descarga: <c>csv</c> o <c>xlsx</c>
    /// (default <c>xlsx</c>). Case-insensitive.</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <response code="200">Devuelve el archivo plantilla con el Content-Type
    /// y Content-Disposition correspondientes.</response>
    /// <response code="400">Formato no soportado (distinto de csv/xlsx).</response>
    /// <response code="404">Modulo sin plantilla disponible (ej:
    /// <c>unidad-medida</c> aun no migrado a v2).</response>
    [HttpGet("{modulo}/plantilla")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DescargarPlantilla(
        [FromRoute] string modulo,
        [FromQuery] string? formato = "xlsx",
        CancellationToken ct = default)
    {
        var moduloNormalizado = (modulo ?? string.Empty).Trim().ToLowerInvariant();
        var formatoNormalizado = (formato ?? "xlsx").Trim().ToLowerInvariant();

        // 1) Modulo no soportado: 404 (cubre tanto modulos inexistentes como
        //    modulos que aun no tienen plantilla v2).
        if (moduloNormalizado != "material")
        {
            _logger.LogInformation(
                "GET /api/import/{Modulo}/plantilla -> 404 (modulo sin plantilla)",
                modulo);
            throw new PlantillaNoDisponibleException(
                moduloNormalizado,
                $"El modulo '{modulo}' no tiene una plantilla de importacion disponible.");
        }

        // 2) Formato invalido: 400. Aceptamos csv y xlsx unicamente.
        if (formatoNormalizado != "csv" && formatoNormalizado != "xlsx")
        {
            _logger.LogInformation(
                "GET /api/import/{Modulo}/plantilla?formato={Formato} -> 400 (formato invalido)",
                modulo, formato);
            throw new FormatoPlantillaInvalidoException(
                formato ?? string.Empty,
                $"El formato '{formato}' no es valido. Use 'csv' o 'xlsx'.");
        }

        _logger.LogInformation(
            "GET /api/import/{Modulo}/plantilla?formato={Formato} -> 200",
            modulo, formatoNormalizado);

        // 3) Generamos el archivo. El controller concentra la logica porque
        //    son dos formatos muy diferentes (CSV con StringBuilder, XLSX
        //    con el helper NPOI); un service intermedio no aporta valor aqui.
        if (formatoNormalizado == "csv")
        {
            var csv = GenerarPlantillaCsv();
            var nombreArchivo = $"plantilla-materiales-{DateTime.Now:yyyyMMdd-HHmm}.csv";
            return Task.FromResult<IActionResult>(
                File(csv, "text/csv; charset=utf-8", nombreArchivo));
        }
        else
        {
            var xlsx = GenerarPlantillaXlsx();
            var nombreArchivo = $"plantilla-materiales-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
            return Task.FromResult<IActionResult>(
                File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo));
        }
    }

    /// <summary>
    /// Genera la plantilla CSV con BOM UTF-8, separador <c>;</c> y una sola
    /// fila de encabezados (sin filas de ejemplo: el operador las agrega
    /// manualmente y asi evitamos publicar datos ficticios en produccion).
    /// </summary>
    private static byte[] GenerarPlantillaCsv()
    {
        // BOM UTF-8 explicito para que Excel detecte acentos castellanos al
        // abrir el archivo. Importante: Encoding.GetBytes(string) NO emite
        // el BOM por si solo aunque la codificacion tenga
        // encoderShouldEmitUTF8Identifier=true (eso solo afecta a los
        // StreamWriter). Hay que concatenar el preambulo manualmente.
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var sb = new StringBuilder();
        sb.Append("Especialidad;Nombre;UnidadMedida;Codigo");
        sb.Append("\r\n");
        // SIN filas de ejemplo por decision de diseno (Fase 4): el operador
        // escribe los datos desde cero. Esto evita que datos ficticios
        // lleguen a produccion por error de copy-paste.
        var body = encoding.GetBytes(sb.ToString());

        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    /// <summary>
    /// Genera la plantilla XLSX usando el helper generico. Una sola fila de
    /// encabezados (negrita, fondo gris) en la primera fila visible, freeze
    /// pane activado.
    /// </summary>
    private byte[] GenerarPlantillaXlsx()
    {
        var config = new ExcelSheetConfig
        {
            SheetName = "Plantilla Materiales",
            Title = "Plantilla de importacion - Materiales",
            FiltersSubtitle = "Borre este archivo antes de cargar datos reales.",
            GeneratedAtSubtitle = "Generado el: {0}",
            IncludeTotalsRow = false,
            HeaderRowIndex = 0
        };

        // Lista vacia: el helper escribe solo los headers.
        return _excelExporter.ExportToXlsx(
            Array.Empty<MaterialImportPlantillaRow>(),
            config);
    }
}
