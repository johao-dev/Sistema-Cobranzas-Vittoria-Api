using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Http;

/// <summary>Traduce el archivo multipart de ASP.NET al archivo que reciben los casos de uso.</summary>
public static class ArchivoSubido
{
    public static async Task<ArchivoTabular> LeerAsync(IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            throw new ArchivoInvalidoException("ARCHIVO_VACIO", "Adjunte un archivo CSV o XLSX con datos.");
        await using var flujo = new MemoryStream();
        await archivo.CopyToAsync(flujo, ct);
        return new ArchivoTabular(archivo.FileName, archivo.ContentType, flujo.ToArray());
    }
}
