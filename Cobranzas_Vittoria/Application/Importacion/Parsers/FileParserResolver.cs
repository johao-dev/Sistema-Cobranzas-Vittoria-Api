using Cobranzas_Vittoria.Application.Importacion.Excepciones;

namespace Cobranzas_Vittoria.Application.Importacion.Parsers;

/// <summary>
/// Resuelve que <see cref="IFileParser"/> usar para un archivo dado, basandose
/// en la extension y los magic numbers del contenido.
///
/// Se registran todas las implementaciones de <see cref="IFileParser"/> en DI
/// y el resolver las prueba en orden hasta encontrar la primera que acepte el archivo.
/// Esto sigue el patron "first match wins" y permite agregar nuevos formatos
/// (ej: ODS, Google Sheets export) sin modificar el controller ni el template method.
/// </summary>
public class FileParserResolver
{
    private readonly IEnumerable<IFileParser> _parsers;

    public FileParserResolver(IEnumerable<IFileParser> parsers)
    {
        _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
    }

    /// <summary>
    /// Detecta el parser adecuado para el archivo. Lee la extension y los primeros
    /// bytes (sin consumir todo el stream) para identificar el formato real.
    /// </summary>
    /// <exception cref="EstructuraInvalidaException">
    /// Si la extension no es una de las soportadas o el contenido no coincide
    /// con ninguno de los parsers registrados.
    /// </exception>
    public IFileParser ObtenerParser(IFormFile file)
    {
        var extension = ObtenerExtension(file.FileName);

        // Leemos los primeros bytes una sola vez y los compartimos con todos los parsers.
        // Importante: NO consumimos el stream principal; abrimos uno nuevo para el sniffing
        // y luego el parser lo abrira de nuevo desde cero.
        using var peek = file.OpenReadStream();
        var buffer = new byte[8];
        var leidos = peek.Read(buffer, 0, buffer.Length);
        var primerosBytes = buffer.AsSpan(0, leidos);

        foreach (var parser in _parsers)
        {
            if (parser.PuedeParsear(extension, primerosBytes))
                return parser;
        }

        throw new EstructuraInvalidaException(
            "FORMATO_INVALIDO",
            $"No se encontro un parser para el archivo '{file.FileName}'. " +
            $"Formatos soportados: .csv, .xlsx, .xls.");
    }

    private static string ObtenerExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;
        var idx = fileName.LastIndexOf('.');
        return idx < 0 ? string.Empty : fileName[idx..].ToLowerInvariant();
    }
}
