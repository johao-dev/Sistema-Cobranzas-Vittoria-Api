using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Middleware
{
    /// <summary>
    /// Captura excepciones de toda la pipeline HTTP y las traduce a una respuesta JSON consistente.
    ///
    /// Categorias manejadas:
    ///   - ArchivoInvalidoException
    ///       - Codigo "TAMANIO_EXCEDIDO"  -> 413 Payload Too Large
    ///       - Resto de codigos            -> 400 BadRequest
    ///   - EstructuraInvalidaException    -> 400 BadRequest
    ///   - DatosInvalidosException         -> 422 Unprocessable Entity (+ lista de errores por fila)
    ///   - ModuloNoSoportadoException      -> 400 BadRequest (codigo "MODULO_NO_SOPORTADO")
    ///   - SqlException                    -> 500 SQL_ERROR        (deuda tecnica documentada)
    ///   - Exception (cualquier otra)      -> 500 UNHANDLED_ERROR  (deuda tecnica documentada)
    ///
    /// Formato de respuesta:
    ///   { "ok": false, "error": "CODIGO", "message": "..." }
    ///   Para DatosInvalidosException se agrega "errores": [ { fila, campo, codigoError, mensaje } ].
    ///
    /// <para>
    /// <b>Logging por nivel</b> (configurable via appsettings.{Env}.json):
    /// <list type="bullet">
    ///   <item><b>400/413</b> (rechazo del cliente, p.ej. extension invalida):
    ///     <c>LogWarning</c>. El nivel se deja a Warning porque en produccion es
    ///     util ver que clientes estan enviando requests malformados. En dev/staging
    ///     el appsettings puede sobrescribir a Information si se quiere mas detalle.</item>
    ///   <item><b>422</b> (DatosInvalidos): <c>LogWarning</c> con metadata
    ///     (cantidad y codigos unicos de errores). NO se incluye el contenido
    ///     de los errores[] para evitar PII en logs.</item>
    ///   <item><b>500</b> (excepcion no controlada): <c>LogError</c> con stack
    ///     trace completo, ruta HTTP y UserAgent.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class ApiExceptionMiddleware : IMiddleware
    {
        private readonly ILogger<ApiExceptionMiddleware> _logger;

        public ApiExceptionMiddleware(ILogger<ApiExceptionMiddleware> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (ArchivoInvalidoException ex) when (ex.Codigo == "TAMANIO_EXCEDIDO")
            {
                _logger.LogWarning(
                    "Rechazo 413 ({Tipo}) en {Method} {Path}: {Codigo} - {Mensaje}",
                    nameof(ArchivoInvalidoException), context.Request.Method, context.Request.Path,
                    ex.Codigo, ex.Message);
                await EscribirErrorAsync(context, StatusCodes.Status413PayloadTooLarge, "TAMANIO_EXCEDIDO", ex.Message);
            }
            catch (ArchivoInvalidoException ex)
            {
                _logger.LogWarning(
                    "Rechazo 400 ({Tipo}) en {Method} {Path}: {Codigo} - {Mensaje}",
                    nameof(ArchivoInvalidoException), context.Request.Method, context.Request.Path,
                    ex.Codigo, ex.Message);
                await EscribirErrorAsync(context, StatusCodes.Status400BadRequest, ex.Codigo, ex.Message);
            }
            catch (EstructuraInvalidaException ex)
            {
                _logger.LogWarning(
                    "Rechazo 400 ({Tipo}) en {Method} {Path}: {Codigo} - {Mensaje}",
                    nameof(EstructuraInvalidaException), context.Request.Method, context.Request.Path,
                    ex.Codigo, ex.Message);
                await EscribirErrorAsync(context, StatusCodes.Status400BadRequest, ex.Codigo, ex.Message);
            }
            catch (DatosInvalidosException ex)
            {
                // 422: logueamos solo metadata (no contenido de filas) para
                // evitar PII en logs. La cantidad y los codigos unicos son
                // suficientes para detectar patrones de fallo en produccion.
                var codigosUnicos = ex.Errores
                    .GroupBy(e => e.CodigoError)
                    .Select(g => $"{g.Key}={g.Count()}")
                    .OrderBy(s => s);
                _logger.LogWarning(
                    "Rechazo 422 (DatosInvalidosException) en {Method} {Path}. TotalErrores={Total} Codigos=[{Codigos}]",
                    context.Request.Method, context.Request.Path, ex.Errores.Count,
                    string.Join(", ", codigosUnicos));
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    ok = false,
                    error = "DATOS_INVALIDOS",
                    message = ex.Message,
                    errores = ex.Errores.Select(e => new
                    {
                        fila = e.Fila,
                        campo = e.Campo,
                        codigoError = e.CodigoError,
                        mensaje = e.Mensaje
                    })
                });
            }
            catch (ModuloNoSoportadoException ex)
            {
                _logger.LogWarning(
                    "Rechazo 400 ({Tipo}) en {Method} {Path}: {Codigo} - {Mensaje}",
                    nameof(ModuloNoSoportadoException), context.Request.Method, context.Request.Path,
                    ModuloNoSoportadoException.CodigoError, ex.Message);
                await EscribirErrorAsync(context, StatusCodes.Status400BadRequest, ModuloNoSoportadoException.CodigoError, ex.Message);
            }
            catch (SqlException ex)
            {
                // Error: fallo de SQL no esperado. En produccion se deberia loguear
                // con detalle y dispararse una alerta.
                _logger.LogError(ex,
                    "SQL_ERROR en {Method} {Path}. Numero={Numero} Remoto={RemoteIp} UserAgent={UserAgent}",
                    context.Request.Method, context.Request.Path, ex.Number,
                    context.Connection.RemoteIpAddress, context.Request.Headers.UserAgent.ToString());
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { ok = false, error = "SQL_ERROR", message = ex.Message });
            }
            catch (Exception ex)
            {
                // 500: cualquier otra excepcion. Logueamos stack trace completo
                // y contexto HTTP (metodo, path, IP, user agent) para diagnosticar
                // en produccion. El response NO expone el stack al cliente.
                _logger.LogError(ex,
                    "UNHANDLED_ERROR ({Tipo}) en {Method} {Path}. Remoto={RemoteIp} UserAgent={UserAgent}",
                    ex.GetType().Name, context.Request.Method, context.Request.Path,
                    context.Connection.RemoteIpAddress, context.Request.Headers.UserAgent.ToString());
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { ok = false, error = "UNHANDLED_ERROR", message = ex.Message });
            }
        }

        private static async Task EscribirErrorAsync(HttpContext context, int statusCode, string codigo, string mensaje)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { ok = false, error = codigo, message = mensaje });
        }
    }
}
