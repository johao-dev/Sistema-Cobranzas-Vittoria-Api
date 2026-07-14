using System.Text;
using Microsoft.AspNetCore.Http;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Common;

/// <summary>
/// Helpers para crear <see cref="IFormFile"/> en memoria, utiles para pruebas
/// unitarias de parsers, validadores y resolvers sin depender del pipeline HTTP.
///
/// Cubre los tres formatos soportados por la feature de importacion:
///   - CSV  (texto plano UTF-8 o Latin-1)
///   - XLSX (Office Open XML, via NPOI)
///   - XLS  (OLE2, via NPOI)
/// </summary>
public static class TestFormFiles
{
    /// <summary>Crea un IFormFile a partir de bytes, nombre de archivo y content type opcional.</summary>
    public static IFormFile FromBytes(byte[] content, string fileName, string? contentType = null)
    {
        var stream = new MemoryStream(content, writable: false);
        return new FormFile(stream, baseStreamOffset: 0, length: content.Length, name: "file", fileName: fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType ?? "application/octet-stream"
        };
    }

    /// <summary>Crea un IFormFile a partir de un string (codificado en UTF-8 por defecto).</summary>
    public static IFormFile FromText(string text, string fileName, string? contentType = null, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        return FromBytes(encoding.GetBytes(text), fileName, contentType ?? "text/csv");
    }

    /// <summary>
    /// Crea un XLSX en memoria con una hoja, fila de encabezados y filas de datos.
    /// </summary>
    public static byte[] BuildXlsx(string[] headers, params string[][] rows)
    {
        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("Sheet1");
        WriteRows(sheet, headers, rows);
        return WriteToBytes(workbook);
    }

    /// <summary>
    /// Crea un XLS (OLE2) en memoria con una hoja, fila de encabezados y filas de datos.
    /// </summary>
    public static byte[] BuildXls(string[] headers, params string[][] rows)
    {
        var workbook = new HSSFWorkbook();
        var sheet = workbook.CreateSheet("Sheet1");
        WriteRows(sheet, headers, rows);
        return WriteToBytes(workbook);
    }

    private static void WriteRows(ISheet sheet, string[] headers, string[][] rows)
    {
        var headerRow = sheet.CreateRow(0);
        for (int i = 0; i < headers.Length; i++)
            headerRow.CreateCell(i).SetCellValue(headers[i]);

        for (int r = 0; r < rows.Length; r++)
        {
            var dataRow = sheet.CreateRow(r + 1);
            for (int c = 0; c < rows[r].Length; c++)
            {
                var cell = dataRow.CreateCell(c);
                var value = rows[r][c];
                // Heuristica: detectar booleanos y numericos para poblar la celda con el tipo correcto.
                if (bool.TryParse(value, out var b))
                    cell.SetCellValue(b);
                else if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n))
                    cell.SetCellValue((double)n);
                else
                    cell.SetCellValue(value);
            }
        }
    }

    private static byte[] WriteToBytes(IWorkbook workbook)
    {
        using var ms = new MemoryStream();
        workbook.Write(ms, leaveOpen: false);
        return ms.ToArray();
    }
}
