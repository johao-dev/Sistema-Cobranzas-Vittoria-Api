using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Cobranzas_Vittoria.Swagger;

/// <summary>
/// Configuracion centralizada de Swagger/OpenAPI para la API.
/// Encapsula la metadata, la inclusion de comentarios XML, los operation filters
/// y los security schemes. El objetivo es que <c>Program.cs</c> no contenga
/// detalles de Swashbuckle y sea facil de testear.
/// </summary>
public static class SwaggerConfiguration
{
    /// <summary>
    /// Configura SwaggerGen con:
    ///   - Metadata de la API (titulo, version, descripcion, contacto).
    ///   - Comentarios XML extraidos del assembly principal (asi los
    ///     <c>&lt;summary&gt;</c> de los controllers y actions aparecen en la UI).
    ///   - OperationFilter global que agrega las respuestas 400/413/422/500
    ///     con shape JSON y ejemplos para todos los endpoints.
    ///   - Mapeo de <see cref="IFormFile"/> a <c>string</c>/<c>binary</c> para
    ///     que Swashbuckle pueda generar el schema del endpoint de upload
    ///     (<c>POST /api/import/{modulo}</c>) sin lanzar
    ///     "Failed to generate Operation for action".
    /// </summary>
    public static IServiceCollection AddImportacionSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            // -----------------------------------------------------------------
            // Metadata del documento OpenAPI
            // -----------------------------------------------------------------
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Cobranzas Vittoria API",
                Version = "v1",
                Description = """
                    API REST del sistema de Cobranzas Vittoria.

                    ## Modulos de importacion masiva

                    El endpoint **`POST /api/import/{modulo}`** acepta archivos CSV / XLSX / XLS
                    y los inserta via Stored Procedure con Table-Valued Parameters.
                    Los modulos soportados son:

                    | Modulo (URL) | Tabla destino |
                    |---|---|
                    | `unidad-medida` | `maestra.UnidadMedida` |
                    | `especialidad` | `maestra.Especialidad` |
                    | `material` | `maestra.Material` |
                    | `proveedor` | `maestra.Proveedor` |
                    | `proveedor-gasto` | `maestra.ProveedorGastoAdministrativo` |
                    | `proveedor-terreno` | `maestra.ProveedorTerreno` |
                    | `categoria-gasto` | `maestra.CategoriaGasto` |

                    **Limitaciones**: tamano maximo 10 MB, maximo 100 filas por archivo.

                    **Atomicidad**: la carga es transaccional; si una sola fila falla,
                    toda la operacion hace rollback.
                    """,
                Contact = new OpenApiContact
                {
                    Name = "Equipo Vittoria",
                    Email = "dev@vittoria.local"
                }
            });

            // -----------------------------------------------------------------
            // Mapeo de tipos no soportados por el schema generator por defecto.
            // Sin esto, el OperationFilter de Swashbuckle revienta en el
            // endpoint de importacion con:
            //   "Failed to generate Operation for action - ImportController.Importar"
            // porque no sabe serializar IFormFile a un JSON Schema.
            // -----------------------------------------------------------------
            options.MapType<IFormFile>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "binary"
            });
            options.MapType<IFormFileCollection>(() => new OpenApiSchema
            {
                Type = "array",
                Items = new OpenApiSchema { Type = "string", Format = "binary" }
            });

            // -----------------------------------------------------------------
            // Comentarios XML: habilitan que los <summary> de actions y
            // controllers aparezcan en la documentacion.
            // -----------------------------------------------------------------
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            // -----------------------------------------------------------------
            // OperationFilter global: inyecta 400/413/422/500 con ejemplos.
            // -----------------------------------------------------------------
            options.OperationFilter<ErrorResponsesOperationFilter>();

            // -----------------------------------------------------------------
            // Tags: agrupa los endpoints en secciones en la UI.
            // -----------------------------------------------------------------
            options.TagActionsBy(api =>
            {
                // Usa el nombre del controller sin el sufijo "Controller" como tag.
                // Ej: "ImportController" -> "Import".
                var controllerName = api.ActionDescriptor.RouteValues["controller"];
                return new[] { controllerName?.Replace("Controller", "") ?? "API" };
            });

            options.DocInclusionPredicate((docName, apiDesc) =>
            {
                // Solo incluye en el doc "v1" las APIs que no tienen otro doc explicito.
                return string.IsNullOrEmpty(apiDesc.GroupName) || apiDesc.GroupName == docName;
            });
        });

        return services;
    }

    /// <summary>
    /// Habilita el middleware de Swagger (UI y JSON).
    /// <b>Solo se debe invocar en entornos Development o Staging.</b> En
    /// Production la superficie de descubrimiento de la API debe estar cerrada
    /// publicamente. La guarda del entorno se hace en <c>Program.cs</c> para
    /// mantener este extension method sin dependencias de <c>IWebHostEnvironment</c>.
    /// </summary>
    public static IApplicationBuilder UseImportacionSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger(c =>
        {
            c.RouteTemplate = "swagger/{documentName}/swagger.json";
        });

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cobranzas Vittoria API v1");
            c.RoutePrefix = "swagger";
            c.DocumentTitle = "Cobranzas Vittoria API - Swagger UI";
            c.DefaultModelsExpandDepth(2);
            c.DefaultModelExpandDepth(2);
            c.DisplayRequestDuration();
            c.EnableDeepLinking();
            c.EnableFilter();
            c.ShowExtensions();
        });

        return app;
    }
}
