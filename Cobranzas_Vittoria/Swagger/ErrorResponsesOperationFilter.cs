using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Cobranzas_Vittoria.Swagger;

/// <summary>
/// Agrega a cada operacion las respuestas de error tipadas que produce el
/// <c>ApiExceptionMiddleware</c>, con ejemplos concretos del shape JSON.
///
/// Se aplica globalmente a TODAS las operaciones. Las respuestas 200 ya las
/// infiere Swashbuckle a partir del tipo de retorno, asi que este filtro solo
/// enriquece los codigos 4xx.
///
/// <para>
/// Codigos cubiertos (todos vienen del middleware, no del action):
/// <list type="bullet">
///   <item><b>400</b> <c>ArchivoInvalidoException</c> (extension/MIME/encoding),
///   <c>EstructuraInvalidaException</c> (encabezados), <c>ModuloNoSoportadoException</c>.</item>
///   <item><b>413</b> <c>ArchivoInvalidoException</c> con codigo <c>TAMANIO_EXCEDIDO</c>.</item>
///   <item><b>422</b> <c>DatosInvalidosException</c> con lista de <c>errores[]</c>.</item>
///   <item><b>500</b> cualquier excepcion no controlada (codigo <c>UNHANDLED_ERROR</c>).</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Nota de robustez:</b> si una operacion rompe el filter (por ejemplo,
/// porque su metadato no es compatible con la forma del error tipado), se
/// loguea a stderr y la generacion de <c>swagger.json</c> continua para
/// las demas operaciones. Asi una operacion mal documentada no tumba
/// toda la API OpenAPI.
/// </para>
/// </summary>
public sealed class ErrorResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Defensa: si una operacion rompe el filter, no debemos tumbar la
        // generacion completa de swagger.json. Logueamos y seguimos.
        try
        {
            ApplyCore(operation, context);
        }
        catch (Exception ex)
        {
            var path = context.ApiDescription.RelativePath ?? "(null)";
            var method = context.ApiDescription.HttpMethod ?? "(null)";
            Console.Error.WriteLine(
                $"[Swagger] ErrorResponsesOperationFilter fallo en {method} /{path}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ApplyCore(OpenApiOperation operation, OperationFilterContext context)
    {
        // 400 - BadRequest: archivo/estructura/modulo invalido.
        // El shape es siempre el mismo: ok, error, message.
        // Variantes por codigo: MODULO_NO_SOPORTADO, ENCABEZADOS_INCORRECTOS,
        // EXTENSION_INVALIDA, MIME_INVALIDO, ARCHIVO_VACIO, ENCODING_INVALIDO.
        operation.Responses["400"] = new OpenApiResponse
        {
            Description = "Solicitud invalida (archivo/estructura/modulo rechazado por el validador o el service).",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = BuildErrorSchema(withErrores: false),
                    Examples = new Dictionary<string, OpenApiExample>
                    {
                        ["MODULO_NO_SOPORTADO"] = new OpenApiExample
                        {
                            Summary = "Modulo no soportado",
                            Value = new OpenApiString(
                                "{\"ok\":false,\"error\":\"MODULO_NO_SOPORTADO\"," +
                                "\"message\":\"El modulo 'foo' no es soportado por la API de importacion. " +
                                "Modulos disponibles: unidad-medida, especialidad, material, ...\"}")
                        },
                        ["ENCABEZADOS_INCORRECTOS"] = new OpenApiExample
                        {
                            Summary = "Faltan columnas requeridas",
                            Value = new OpenApiString(
                                "{\"ok\":false,\"error\":\"ENCABEZADOS_INCORRECTOS\"," +
                                "\"message\":\"Faltan las siguientes columnas requeridas: Codigo, Nombre. " +
                                "Encabezados recibidos: Foo, Bar.\"}")
                        },
                        ["EXTENSION_INVALIDA"] = new OpenApiExample
                        {
                            Summary = "Extension no permitida (.txt, .pdf, etc.)",
                            Value = new OpenApiString(
                                "{\"ok\":false,\"error\":\"EXTENSION_INVALIDA\"," +
                                "\"message\":\"La extension '.txt' no esta permitida. Use .csv, .xlsx o .xls.\"}")
                        }
                    }
                }
            }
        };

        // 413 - Payload Too Large
        operation.Responses["413"] = new OpenApiResponse
        {
            Description = "El archivo excede el tamano maximo permitido (10 MB).",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = BuildErrorSchema(withErrores: false),
                    Example = new OpenApiString(
                        "{\"ok\":false,\"error\":\"TAMANIO_EXCEDIDO\"," +
                        "\"message\":\"El archivo pesa 11.5 MB, excede el maximo de 10 MB.\"}")
                }
            }
        };

        // 422 - Unprocessable Entity (validacion por fila + errores del SP).
        // Shape INCLUYE el array "errores" con detalle por fila.
        operation.Responses["422"] = new OpenApiResponse
        {
            Description = "Una o mas filas del archivo fallaron la validacion o el SP rechazo los datos. " +
                          "La operacion es atomica: ninguna fila se inserto (rollback).",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = BuildErrorSchema(withErrores: true),
                    Example = new OpenApiString(
                        "{\"ok\":false,\"error\":\"DATOS_INVALIDOS\"," +
                        "\"message\":\"2 fila(s) con errores.\"," +
                        "\"errores\":[" +
                        "{\"fila\":2,\"campo\":\"\",\"codigoError\":\"CAMPO_REQUERIDO\"," +
                        "\"mensaje\":\"La columna 'Codigo' es requerida y no puede estar vacia.\"}," +
                        "{\"fila\":3,\"campo\":\"\",\"codigoError\":\"VALOR_DUPLICADO_EN_ARCHIVO\"," +
                        "\"mensaje\":\"El codigo 'BAL' aparece mas de una vez en el archivo.\"}" +
                        "]}")
                }
            }
        };

        // 500 - Unhandled error (cualquier excepcion no controlada)
        operation.Responses["500"] = new OpenApiResponse
        {
            Description = "Error inesperado del servidor. La operacion no se completo.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = BuildErrorSchema(withErrores: false),
                    Example = new OpenApiString(
                        "{\"ok\":false,\"error\":\"UNHANDLED_ERROR\"," +
                        "\"message\":\"Error interno del servidor. Contactese con el administrador.\"}")
                }
            }
        };
    }

    /// <summary>
    /// Construye un schema OpenAPI para la respuesta de error tipada.
    /// Si <paramref name="withErrores"/> es true, incluye la propiedad
    /// <c>errores</c> como array de objetos (caso 422).
    /// </summary>
    private static OpenApiSchema BuildErrorSchema(bool withErrores)
    {
        var schema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["ok"] = new OpenApiSchema { Type = "boolean", Example = new OpenApiBoolean(false) },
                ["error"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Codigo de error legible para clientes. Ver Swagger schema para la lista completa."
                },
                ["message"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Mensaje legible para humanos."
                }
            },
            Required = new HashSet<string> { "ok", "error", "message" }
        };

        if (withErrores)
        {
            schema.Properties["errores"] = new OpenApiSchema
            {
                Type = "array",
                Description = "Detalle de errores por fila (solo presente en 422).",
                Items = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["fila"] = new OpenApiSchema { Type = "integer", Format = "int32" },
                        ["campo"] = new OpenApiSchema { Type = "string" },
                        ["codigoError"] = new OpenApiSchema { Type = "string" },
                        ["mensaje"] = new OpenApiSchema { Type = "string" }
                    }
                }
            };
        }

        return schema;
    }
}
