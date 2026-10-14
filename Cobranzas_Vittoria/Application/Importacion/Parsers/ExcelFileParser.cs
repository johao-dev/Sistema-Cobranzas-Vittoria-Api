using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Domain.Importacion;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Cobranzas_Vittoria.Application.Importacion.Parsers;

/// <summary>
/// Parser de archivos Excel (.xlsx y .xls) usando NPOI.
/// NPOI soporta ambos formatos con la misma interfaz <see cref="IWorkbook"/>:
///   - .xlsx (OOXML) -> <see cref="XSSFWorkbook"/>
///   - .xls  (OLE2)   -> <see cref="HSSFWorkbook"/>
///
/// Reglas:
///   - Lee la primera hoja del libro. Si no hay hojas -> error.
///   - Primera fila = encabezados. Filas vacias se omiten.
///   - La conversion de celdas a string se hace segun el tipo:
///     numericas, booleanas, fechas y string se normalizan a su representacion textual.
/// </summary>
public class ExcelFileParser : IFileParser
{
    /// <summary>
    /// Lee una plantilla XLSX con hoja, fila de encabezados y orden contractual explícitos.
    /// Conserva el número físico de fila y el valor numérico OOXML, sin pasar importes por double.
    /// Las columnas auxiliares no determinan si existe un registro.
    /// </summary>
    public List<SpreadsheetRow> ParsePlantilla(IFormFile file, string hoja, int filaEncabezados,
        IReadOnlyList<string> encabezados, IReadOnlySet<int> auxiliares)
    {
        using var stream = file.OpenReadStream();
        using var workbook = CrearWorkbook(file.FileName, stream);
        var sheet = workbook.GetSheet(hoja)
            ?? throw new EstructuraInvalidaException("ENCABEZADOS_INCORRECTOS", $"Falta la hoja '{hoja}'.");
        var header = sheet.GetRow(filaEncabezados - 1);
        if (header is null || encabezados.Where((nombre, i) =>
                header.GetCell(i)?.CellType != CellType.String || header.GetCell(i).StringCellValue != nombre).Any()
            || header.Cells.Any(c => c.ColumnIndex >= encabezados.Count && !string.IsNullOrWhiteSpace(CellToString(c))))
            throw new EstructuraInvalidaException("ENCABEZADOS_INCORRECTOS",
                $"La fila {filaEncabezados} de '{hoja}' debe contener exactamente, en orden: {string.Join(", ", encabezados)}.");

        var filas = new List<SpreadsheetRow>();
        for (var i = filaEncabezados; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row is null) continue;
            var celdas = encabezados.Select((nombre, col) => (nombre, col))
                .Where(c => !auxiliares.Contains(c.col))
                .ToDictionary(c => c.nombre, c => ValorPlantilla(row.GetCell(c.col)), StringComparer.OrdinalIgnoreCase);
            if (row.Cells.Any(c => c.ColumnIndex >= encabezados.Count && !string.IsNullOrWhiteSpace(ValorPlantilla(c))))
                throw new EstructuraInvalidaException("ENCABEZADOS_INCORRECTOS", $"Fila {i + 1}: contiene columnas adicionales.");
            if (celdas.Values.All(string.IsNullOrEmpty)) continue;
            filas.Add(new SpreadsheetRow(i + 1, celdas));
        }
        return filas;
    }

    private static string ValorPlantilla(ICell? cell)
    {
        if (cell is null) return string.Empty;
        var tipo = cell.CellType == CellType.Formula ? cell.CachedFormulaResultType : cell.CellType;
        try
        {
            return tipo switch
            {
                CellType.String => (cell.StringCellValue ?? string.Empty).Trim(),
                CellType.Blank => string.Empty,
                CellType.Numeric when DateUtil.IsCellDateFormatted(cell) =>
                    cell.DateCellValue?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? "#FECHA_INVALIDA",
                CellType.Numeric => NumeroPlantilla((XSSFCell)cell),
                CellType.Boolean => cell.BooleanCellValue ? "true" : "false",
                _ => "#CELDA_INVALIDA"
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
        {
            return "#CELDA_INVALIDA";
        }
    }

    private static string NumeroPlantilla(XSSFCell cell)
    {
        // Excel conserva 15 dígitos significativos. El OOXML de la plantilla contiene, por ejemplo,
        // 3.7599999999999998 para 3.76. Normalizar esa representación usa solo aritmética decimal.
        // Las celdas de texto se mantienen exactas y se validan sin este redondeo.
        var raw = cell.GetRawValue();
        if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var numero))
            return "#NUMERO_INVALIDO";
        return numero.ToString("G15", System.Globalization.CultureInfo.InvariantCulture);
    }
    public const string CodigoFormatoInvalido = "FORMATO_INVALIDO";

    // Magic numbers
    private static readonly byte[] XlsxMagic = { 0x50, 0x4B, 0x03, 0x04 }; // PK..
    private static readonly byte[] XlsMagic  = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

    public string Formato => "xlsx/xls";

    public bool PuedeParsear(string extension, ReadOnlySpan<byte> primerosBytes)
    {
        if (extension == ".xlsx")
        {
            return primerosBytes.Length >= 4
                && primerosBytes[0] == XlsxMagic[0] && primerosBytes[1] == XlsxMagic[1]
                && primerosBytes[2] == XlsxMagic[2] && primerosBytes[3] == XlsxMagic[3];
        }
        if (extension == ".xls")
        {
            if (primerosBytes.Length < XlsMagic.Length) return false;
            for (int i = 0; i < XlsMagic.Length; i++)
                if (primerosBytes[i] != XlsMagic[i]) return false;
            return true;
        }
        return false;
    }

    public List<SpreadsheetRow> Parse(IFormFile file)
    {
        IWorkbook workbook;
        try
        {
            using var stream = file.OpenReadStream();
            workbook = CrearWorkbook(file.FileName, stream);
        }
        catch (Exception ex) when (ex is not EstructuraInvalidaException)
        {
            throw new EstructuraInvalidaException(
                CodigoFormatoInvalido,
                $"El archivo Excel esta danado o no se puede leer: {ex.Message}");
        }

        try
        {
            if (workbook.NumberOfSheets == 0)
                throw new EstructuraInvalidaException(
                    CodigoFormatoInvalido,
                    "El archivo Excel no contiene hojas.");

            var sheet = workbook.GetSheetAt(0);
            if (sheet == null || sheet.LastRowNum < 0)
                return new List<SpreadsheetRow>();

            // Primera fila = encabezados
            var headerRow = sheet.GetRow(0);
            if (headerRow == null)
                throw new EstructuraInvalidaException(
                    CodigoFormatoInvalido,
                    "La primera fila del archivo Excel debe contener los encabezados.");

            var headers = LeerEncabezados(headerRow);
            var filas = new List<SpreadsheetRow>();
            var numeroFila = 1; // 1-based, primera fila de datos = 1

            for (int i = 1; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                var celdas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var filaVacia = true;

                for (int col = 0; col < headers.Length; col++)
                {
                    var header = headers[col];
                    if (string.IsNullOrWhiteSpace(header)) continue;
                    var cell = row.GetCell(col);
                    var valor = CellToString(cell);
                    celdas[header] = valor;
                    if (!string.IsNullOrEmpty(valor)) filaVacia = false;
                }

                if (!filaVacia)
                {
                    filas.Add(new SpreadsheetRow(numeroFila, celdas));
                    numeroFila++;
                }
            }

            return filas;
        }
        finally
        {
            workbook.Close();
        }
    }

    private static IWorkbook CrearWorkbook(string fileName, Stream stream)
    {
        // NPOI detecta el formato por contenido, no por extension.
        // Devolvemos un mensaje claro si no puede crear el workbook.
        try
        {
            if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return new XSSFWorkbook(stream);
            if (fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                return new HSSFWorkbook(stream);
        }
        catch (Exception ex)
        {
            throw new EstructuraInvalidaException(
                CodigoFormatoInvalido,
                $"No se pudo abrir el archivo Excel: {ex.Message}");
        }
        throw new EstructuraInvalidaException(
            CodigoFormatoInvalido,
            "La extension del archivo no es .xlsx ni .xls.");
    }

    private static string[] LeerEncabezados(IRow headerRow)
    {
        var headers = new string[headerRow.LastCellNum];
        for (int i = 0; i < headerRow.LastCellNum; i++)
        {
            var cell = headerRow.GetCell(i);
            headers[i] = CellToString(cell)?.Trim() ?? string.Empty;
        }
        return headers;
    }

    /// <summary>
    /// Convierte una celda de NPOI a su representacion como string.
    /// Fechas se devuelven en formato ISO 8601 (yyyy-MM-dd) o (yyyy-MM-dd HH:mm:ss)
    /// si tienen hora. Booleanos como "true"/"false". Numeros con invariant culture.
    /// </summary>
    private static string CellToString(ICell? cell)
    {
        if (cell == null) return string.Empty;

        return cell.CellType switch
        {
            CellType.String  => cell.StringCellValue ?? string.Empty,
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                                ? FormatearFecha(cell.NumericCellValue)
                                : cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            CellType.Boolean => cell.BooleanCellValue ? "true" : "false",
            CellType.Formula => LeerFormula(cell),
            CellType.Blank   => string.Empty,
            _                => string.Empty,
        };
    }

    private static string LeerFormula(ICell cell)
    {
        try
        {
            return cell.CellType == CellType.String
                ? (cell.StringCellValue ?? string.Empty)
                : (cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FormatearFecha(double serial)
    {
        try
        {
            var dt = DateTime.FromOADate(serial);
            // Si tiene hora distinta de 00:00:00, incluirla.
            return dt.TimeOfDay == TimeSpan.Zero
                ? dt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                : dt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return serial.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
