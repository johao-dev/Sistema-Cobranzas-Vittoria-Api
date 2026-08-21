using System.Globalization;
using System.Text;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Domain.Importacion;
using CsvHelper;
using CsvHelper.Configuration;

namespace Cobranzas_Vittoria.Application.Importacion.Parsers;

/// <summary>
/// Parser de archivos CSV usando <see cref="CsvHelper"/>.
///
/// Reglas (alineadas con el plan de Fase 3 del feature v2):
///   - Delimitador detectado automaticamente:
///       * Si la primera linea tiene mas ";" que ",", se usa ";".
///       * Si tiene mas "," que ";", se usa ",".
///       * Si no tiene ninguno de los dos, se usa ";" por default (convencion
///         del proyecto para Excel en espanol).
///       * Tabulador y pipe se rechazan con <see cref="EstructuraInvalidaException"/>
///         codigo "FORMATO_INVALIDO" (delimitadores no soportados).
///   - Codificacion dual:
///       * Se intenta UTF-8 estricto primero (lanzara
///         <see cref="EstructuraInvalidaException"/> codigo "CODIFICACION_INVALIDO"
///         si el contenido no es UTF-8 valido).
///       * Si el archivo tiene BOM UTF-8 (EF BB BF), siempre se trata como UTF-8.
///       * Si no tiene BOM y los bytes NO son UTF-8 valido, se reintenta como
///         Windows-1252 (Latin-1, superconjunto de ISO-8859-1, cubre acentos
///         castellanos). Este es el formato tipico de exportacion desde Excel
///         "guardar como CSV" en Windows en espanol.
///   - Primera fila = encabezados. Las filas vacias se omiten.
///   - Los nombres de encabezado se preservan tal cual (case-sensitive en el
///     archivo, case-insensitive en <see cref="SpreadsheetRow"/>).
///   - Si los primeros bytes del archivo coinciden con un magic number conocido
///     de otro formato (PDF, ZIP, OLE2, PNG, etc.), se rechaza aunque la
///     extension sea .csv.
/// </summary>
public class CsvFileParser : IFileParser
{
    public const string CodigoDelimitadorInvalido = "FORMATO_INVALIDO";
    public const string CodigoEncodingInvalido = "CODIFICACION_INVALIDO";

    private const char DelimitadorDefault = ';';
    private const char DelimitadorComa = ',';
    private const int MaxBytesParaSniffing = 4096;

    /// <summary>
    /// Magic numbers de formatos que NO son CSV. Si los primeros bytes del archivo
    /// coinciden con alguno, rechazamos el parseo aunque la extension sea .csv.
    /// Esto evita que un binario renombrado (PDF, ZIP, OLE2/XLS, imagen, etc.) termine
    /// siendo procesado por CsvHelper con resultados incorrectos o excepciones internas.
    /// </summary>
    private static readonly byte[][] MagicNumbersNoCsv =
    {
        new byte[] { 0x25, 0x50, 0x44, 0x46 },                                  // %PDF
        new byte[] { 0x50, 0x4B, 0x03, 0x04 },                                  // PK.. (ZIP / XLSX / DOCX)
        new byte[] { 0x50, 0x4B, 0x05, 0x06 },                                  // PK.. (empty ZIP)
        new byte[] { 0x50, 0x4B, 0x07, 0x08 },                                  // PK.. (spanned ZIP)
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 },          // OLE2 (XLS / DOC legacy)
        new byte[] { 0x1F, 0x8B },                                               // GZIP
        new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 },                       // RAR v1.5+
        new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C },                       // 7-Zip
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },          // PNG
        new byte[] { 0xFF, 0xD8, 0xFF },                                         // JPEG (SOI marker)
        new byte[] { 0x47, 0x49, 0x46, 0x38 },                                   // GIF87a/89a
        new byte[] { 0x42, 0x4D },                                               // BMP
        new byte[] { 0x7F, 0x45, 0x4C, 0x46 },                                   // ELF (Linux executables)
        new byte[] { 0xCA, 0xFE, 0xBA, 0xBE },                                   // Java class / Mach-O fat
    };

    public string Formato => "csv";

    public bool PuedeParsear(string extension, ReadOnlySpan<byte> primerosBytes)
    {
        if (extension != ".csv") return false;
        if (EsMagicNumberNoCsv(primerosBytes)) return false;
        // CSV: aceptamos si los primeros bytes son texto (ASCII printable, tab, CR, LF)
        // o si arrancan con BOM UTF-8 (EF BB BF). Esto evita confundir binarios
        // disfrazados de .csv con CSVs reales.
        if (primerosBytes.Length >= 3 &&
            primerosBytes[0] == 0xEF && primerosBytes[1] == 0xBB && primerosBytes[2] == 0xBF)
            return true;

        for (int i = 0; i < primerosBytes.Length; i++)
        {
            var b = primerosBytes[i];
            // Permitir: tab (9), LF (10), CR (13), printable ASCII (32-126), y bytes altos
            // (>= 128) que seran UTF-8 multibyte o parte de Windows-1252.
            if (b == 0x09 || b == 0x0A || b == 0x0D) continue;
            if (b >= 0x20 && b <= 0x7E) continue;
            if (b >= 0x80) continue; // byte alto: probablemente multibyte de algun encoding
            return false;
        }
        return true;
    }

    public List<SpreadsheetRow> Parse(IFormFile file)
    {
        // Leemos los primeros bytes para detectar delimitador y codificacion
        // antes de delegar a CsvHelper.
        using var peekStream = file.OpenReadStream();
        var sniffBuffer = new byte[MaxBytesParaSniffing];
        var bytesLeidos = peekStream.Read(sniffBuffer, 0, sniffBuffer.Length);

        ValidarContenidoCsv(sniffBuffer.AsSpan(0, bytesLeidos));
        var encoding = DetectarEncoding(sniffBuffer.AsSpan(0, bytesLeidos));

        // Decodificamos el sniff en texto para inspeccionar la primera linea y
        // detectar delimitador. Usamos el encoding detectado.
        var textoSniff = encoding.GetString(sniffBuffer, 0, bytesLeidos);
        var delimitador = DetectarDelimitador(textoSniff);

        // Releemos el archivo desde el principio con la codificacion detectada.
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimitador.ToString(),
            HasHeaderRecord = true,
            BadDataFound = null,            // No abortar por campos malformados
            MissingFieldFound = null,        // No abortar por columnas faltantes
            TrimOptions = TrimOptions.Trim,
        };

        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader())
            return new List<SpreadsheetRow>();

        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var filas = new List<SpreadsheetRow>();
        var numeroFila = 1; // 1-based, primera fila de datos = 1

        while (csv.Read())
        {
            var celdas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var filaVacia = true;

            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i];
                if (string.IsNullOrWhiteSpace(header)) continue;
                var valor = i < csv.Parser.Count ? csv.GetField(i) ?? string.Empty : string.Empty;
                celdas[header] = valor;
                if (!string.IsNullOrEmpty(valor)) filaVacia = false;
            }

            // Omitir filas completamente vacias.
            if (!filaVacia)
            {
                filas.Add(new SpreadsheetRow(numeroFila, celdas));
                numeroFila++;
            }
        }

        return filas;
    }

    private static bool EsMagicNumberNoCsv(ReadOnlySpan<byte> bytes)
    {
        foreach (var magic in MagicNumbersNoCsv)
        {
            if (bytes.Length < magic.Length) continue;
            var coincide = true;
            for (int i = 0; i < magic.Length; i++)
            {
                if (bytes[i] != magic[i]) { coincide = false; break; }
            }
            if (coincide) return true;
        }
        return false;
    }

    private static void ValidarContenidoCsv(ReadOnlySpan<byte> sniff)
    {
        // Verifica una sola cosa en un recorrido sobre los primeros bytes del archivo:
        //   1. Que no haya bytes de control invalidos (distintos de \t, \n, \r).
        // La validacion de delimitador ya no se hace aqui; se detecta en
        // DetectarDelimitador y se rechaza tab/pipe que no esten soportados.
        for (int i = 0; i < sniff.Length; i++)
        {
            var b = sniff[i];

            // BOM UTF-8 -> ignorar
            if (b == 0xEF || b == 0xBB || b == 0xBF) continue;

            // Caracteres de control validos en texto CSV (tabulador, salto de
            // linea y retorno de carro). El tabulador NO es un delimitador
            // soportado, pero pasa la validacion binaria; el rechazo final
            // ocurre en DetectarDelimitador con un mensaje mas claro.
            if (b == 0x09 || b == 0x0A || b == 0x0D) continue;

            // Printable ASCII o byte alto (UTF-8 multibyte o Windows-1252) -> OK.
            if (b >= 0x20 && b <= 0x7E) continue;
            if (b >= 0x80) continue;

            // Cualquier otro byte de control es invalido en CSV de texto.
            throw new EstructuraInvalidaException(
                CodigoDelimitadorInvalido,
                $"El archivo CSV contiene un byte de control invalido (0x{b:X2}). " +
                "Esto sugiere que el archivo no es texto CSV valido.");
        }
    }

    private static Encoding DetectarEncoding(ReadOnlySpan<byte> sniff)
    {
        // UTF-8 con BOM -> UTF-8 con BOM.
        if (sniff.Length >= 3 && sniff[0] == 0xEF && sniff[1] == 0xBB && sniff[2] == 0xBF)
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        // Sin BOM: intentamos UTF-8 estricto. Si falla, fallback a Windows-1252
        // (superconjunto de ISO-8859-1; cubre acentos castellanos y se usa en
        // la exportacion por defecto de Excel en espanol sobre Windows).
        try
        {
            // StrictMode = true (default en .NET 8) hace que bytes invalidos lancen.
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            _ = strictUtf8.GetCharCount(sniff.ToArray());
            return strictUtf8;
        }
        catch (DecoderFallbackException)
        {
            // Verificamos que los bytes al menos sean texto (no binarios) antes
            // de aceptar Windows-1252. La validacion de bytes de control ya se
            // hizo en ValidarContenidoCsv.
            return Encoding.GetEncoding("Windows-1252", EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
        }
    }

    /// <summary>
    /// Detecta el delimitador del CSV contando las ocurrencias de ';' y ',' en
    /// la primera linea del archivo (los headers). Reglas:
    ///   - Si la primera linea tiene mas ';' que ',' -> ';'.
    ///   - Si tiene mas ',' que ';' -> ','.
    ///   - Si no tiene ninguno -> ';' por default (convencion del proyecto).
    ///   - Si la primera linea tiene '\t' o '|' -> lanzar FORMATO_INVALIDO.
    /// </summary>
    private static char DetectarDelimitador(string primeraLinea)
    {
        // Consideramos solo la primera linea (sin el BOM que .NET ya remueve).
        var idxSalto = primeraLinea.IndexOfAny(new[] { '\n', '\r' });
        var linea = idxSalto < 0 ? primeraLinea : primeraLinea[..idxSalto];

        // Rechazo temprano: tab y pipe no son soportados.
        if (linea.Contains('\t'))
            throw new EstructuraInvalidaException(
                CodigoDelimitadorInvalido,
                "El delimitador del archivo CSV no puede ser tabulador (\\t). Use ';' o ','.");
        if (linea.Contains('|'))
            throw new EstructuraInvalidaException(
                CodigoDelimitadorInvalido,
                "El delimitador del archivo CSV no puede ser pipe ('|'). Use ';' o ','.");

        var countPuntoYComa = linea.Count(c => c == DelimitadorDefault);
        var countComa = linea.Count(c => c == DelimitadorComa);

        if (countPuntoYComa == 0 && countComa == 0)
            return DelimitadorDefault; // Default del proyecto.

        return countPuntoYComa >= countComa
            ? DelimitadorDefault
            : DelimitadorComa;
    }
}
