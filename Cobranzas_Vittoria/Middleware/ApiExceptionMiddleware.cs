using Cobranzas_Vittoria.Application.Common.Excepciones;
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
    ///   - DatosInvalidosValidacionException -> 422 Unprocessable Entity (+ lista de errores generica;
    ///                                       usado por modulos que no son Importacion, ej: Inventario)
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
                await EscribirRespuestaValidacionAsync(
                    context,
                    tipoOrigen: "DatosInvalidosException",
                    message: ex.Message,
                    erroresSerializables: ex.Errores.Select(e => new
                    {
                        fila = e.Fila,
                        campo = e.Campo,
                        codigoError = e.CodigoError,
                        mensaje = e.Mensaje
                    }),
                    codigosUnicos: ex.Errores
                        .GroupBy(e => e.CodigoError)
                        .Select(g => $"{g.Key}={g.Count()}")
                        .OrderBy(s => s));
            }
            catch (DatosInvalidosValidacionException ex)
            {
                // Misma respuesta 422 que DatosInvalidosException (de Importacion),
                // pero para modulos que usan el modelo generico de Common
                // (ej: Inventario, Kardex). El campo `fila` puede ser null
                // (no aplica a payloads HTTP) y se serializa como tal.
                await EscribirRespuestaValidacionAsync(
                    context,
                    tipoOrigen: nameof(DatosInvalidosValidacionException),
                    message: ex.Message,
                    erroresSerializables: ex.Errores.Select(e => new
                    {
                        fila = e.Fila,
                        campo = e.Campo,
                        codigoError = e.CodigoError,
                        mensaje = e.Mensaje
                    }),
                    codigosUnicos: ex.Errores
                        .GroupBy(e => e.CodigoError)
                        .Select(g => $"{g.Key}={g.Count()}")
                        .OrderBy(s => s));
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

        /// <summary>
        /// Serializa una respuesta 422 a partir de una coleccion ya proyectada
        /// de errores. Reusado por los catches de <see cref="DatosInvalidosException"/>
        /// (Importacion) y <see cref="DatosInvalidosValidacionException"/>
        /// (Common, ej: Inventario). Asi ambos tipos producen la misma forma
        /// de respuesta sin acoplar el middleware a namespaces de modulo.
        /// </summary>
        /// <param name="erroresSerializables">
        /// Errores ya proyectados al anonimo con campos
        /// <c>{ fila, campo, codigoError, mensaje }</c>.
        /// </param>
        /// <param name="codigosUnicos">
        /// Lista de <c>"CODIGO=cantidad"</c> para el log (no se expone al cliente
        /// para evitar PII, solo metadata de patrones).
        /// </param>
        private async Task EscribirRespuestaValidacionAsync(
            HttpContext context,
            string tipoOrigen,
            string message,
            IEnumerable<object> erroresSerializables,
            IEnumerable<string> codigosUnicos)
        {
            var lista = erroresSerializables as IList<object> ?? erroresSerializables.ToList();
            var codigos = codigosUnicos as IList<string> ?? codigosUnicos.ToList();

            _logger.LogWarning(
                "Rechazo 422 ({TipoOrigen}) en {Method} {Path}. TotalErrores={Total} Codigos=[{Codigos}]",
                tipoOrigen, context.Request.Method, context.Request.Path, lista.Count,
                string.Join(", ", codigos));

            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                ok = false,
                error = "DATOS_INVALIDOS",
                message = message,
                errores = lista
            });
        }
    }
}
