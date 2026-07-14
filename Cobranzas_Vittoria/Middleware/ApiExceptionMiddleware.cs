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
    /// </summary>
    public class ApiExceptionMiddleware : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (ArchivoInvalidoException ex) when (ex.Codigo == "TAMANIO_EXCEDIDO")
            {
                await EscribirErrorAsync(context, StatusCodes.Status413PayloadTooLarge, "TAMANIO_EXCEDIDO", ex.Message);
            }
            catch (ArchivoInvalidoException ex)
            {
                await EscribirErrorAsync(context, StatusCodes.Status400BadRequest, ex.Codigo, ex.Message);
            }
            catch (EstructuraInvalidaException ex)
            {
                await EscribirErrorAsync(context, StatusCodes.Status400BadRequest, ex.Codigo, ex.Message);
            }
            catch (DatosInvalidosException ex)
            {
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
                await EscribirErrorAsync(context, StatusCodes.Status400BadRequest, ModuloNoSoportadoException.CodigoError, ex.Message);
            }
            catch (SqlException ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { ok = false, error = "SQL_ERROR", message = ex.Message });
            }
            catch (Exception ex)
            {
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
