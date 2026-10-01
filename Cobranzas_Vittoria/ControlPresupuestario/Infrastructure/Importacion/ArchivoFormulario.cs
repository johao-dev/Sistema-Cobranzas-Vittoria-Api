using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Importacion;

/// <summary>Reconstruye un IFormFile a partir del archivo del caso de uso, para reutilizar el motor de importación.</summary>
internal static class ArchivoFormulario
{
    public static IFormFile Crear(ArchivoTabular archivo)
    {
        var flujo = new MemoryStream(archivo.Contenido, writable: false);
        return new FormFile(flujo, 0, archivo.Contenido.LongLength, "archivo", archivo.NombreArchivo)
        {
            Headers = new HeaderDictionary(),
            ContentType = archivo.ContentType ?? "application/octet-stream"
        };
    }
}
