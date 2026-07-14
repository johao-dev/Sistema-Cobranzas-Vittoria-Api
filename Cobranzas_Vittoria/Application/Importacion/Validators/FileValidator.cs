using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Microsoft.AspNetCore.Http;

namespace Cobranzas_Vittoria.Application.Importacion.Validators;

/// <summary>
/// Valida el archivo de importacion ANTES de ser parseado.
///
/// Verifica (en orden):
///   1. Que el archivo no sea nulo y que tenga contenido (no vacio).
///   2. Que la extension este en la whitelist (.csv, .xlsx, .xls).
///   3. Que el tamano no exceda el maximo permitido (10 MB).
///   4. Que el MIME declarado por el cliente sea consistente con la extension.
///
/// NOTA: la validacion de magic numbers (firma binaria del contenido) NO se hace aqui;
/// la realiza cada <c>IFileParser.PuedeParsear</c> al ser invocado por el
/// <c>FileParserResolver</c>. Esto evita duplicar logica.
///
/// Esta clase NO conoce DTOs ni SPs: solo valida el archivo en si.
/// </summary>
public class FileValidator
{
    /// <summary>Maximo 10 MB segun los requisitos del feature.</summary>
    public const long MaximoTamanioBytes = 10L * 1024L * 1024L;

    private static readonly HashSet<string> ExtensionesPermitidas =
        new(StringComparer.OrdinalIgnoreCase) { ".csv", ".xlsx", ".xls" };

    // MIME permitidos por extension. La lista NO es exhaustiva (los navegadores
    // reportan distintos valores segun SO y version) pero cubre los principales.
    private static readonly Dictionary<string, string[]> MimePorExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csv"]  = new[] { "text/csv", "application/csv", "application/vnd.ms-excel", "text/plain" },
        [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/octet-stream" },
        [".xls"]  = new[] { "application/vnd.ms-excel", "application/msexcel", "application/octet-stream" },
    };

    /// <summary>
    /// Valida el archivo. Lanza <see cref="ArchivoInvalidoException"/> con el codigo
    /// correspondiente si alguna regla falla.
    /// </summary>
    public void Validar(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            throw new ArchivoInvalidoException("ARCHIVO_VACIO", "No se recibio ningun archivo o el archivo esta vacio.");

        ValidarTamanio(file.Length);
        ValidarExtension(file.FileName);
        ValidarMime(file.FileName, file.ContentType);
    }

    private static void ValidarTamanio(long bytes)
    {
        if (bytes > MaximoTamanioBytes)
        {
            var mb = bytes / 1024d / 1024d;
            throw new ArchivoInvalidoException(
                "TAMANIO_EXCEDIDO",
                $"El archivo supera el tamano maximo permitido de 10 MB. Tamano actual: {mb:F2} MB.");
        }
    }

    private static void ValidarExtension(string fileName)
    {
        var ext = ObtenerExtension(fileName);
        if (!ExtensionesPermitidas.Contains(ext))
        {
            throw new ArchivoInvalidoException(
                "EXTENSION_INVALIDA",
                $"La extension '{ext}' no es valida. Solo se permiten: .csv, .xlsx, .xls.");
        }
    }

    private static void ValidarMime(string fileName, string? contentType)
    {
        var ext = ObtenerExtension(fileName);
        // application/octet-stream es un MIME "generico" que algunos navegadores
        // envian para archivos binarios. Lo aceptamos como valido (no es prueba
        // de nada, pero tampoco es una falsificacion evidente).
        if (string.IsNullOrEmpty(contentType) || contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return;

        if (!MimePorExtension.TryGetValue(ext, out var mimesEsperados))
            return; // Extension no estaba en la whitelist, pero ValidarExtension ya fallo antes.

        foreach (var mime in mimesEsperados)
        {
            if (mime.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                continue;
            if (contentType.Equals(mime, StringComparison.OrdinalIgnoreCase))
                return;
        }

        throw new ArchivoInvalidoException(
            "MIME_INVALIDO",
            $"El MIME declarado '{contentType}' no coincide con la extension '{ext}' del archivo.");
    }

    private static string ObtenerExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;
        var idx = fileName.LastIndexOf('.');
        return idx < 0 ? string.Empty : fileName[idx..].ToLowerInvariant();
    }
}
