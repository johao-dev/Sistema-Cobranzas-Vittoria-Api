using System.Text;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Cobranzas_Vittoria.Tests.Integration.Common;

/// <summary>
/// Helpers para generar archivos de importacion (CSV / XLSX) en memoria
/// para los tests de integracion del <c>ImportController</c>.
///
/// No escribimos a disco para no depender del directorio temporal ni
/// contaminar el contenedor de Testcontainers con uploads.
/// </summary>
public static class ImportFileBuilder
{
    /// <summary>
    /// Genera un CSV con cada string de <paramref name="filas"/> como una linea
    /// (la primera es el encabezado, el resto son datos). Separador fijo: coma.
    /// Codificacion UTF-8 sin BOM.
    /// </summary>
    /// <remarks>
    /// No usamos un parametro <c>separador</c> explicito porque generaria
    /// ambiguedad con <c>params</c>: una llamada como
    /// <c>BuildCsv("Codigo,Nombre")</c> se interpretaria como
    /// <c>separador = "Codigo,Nombre"</c> y <c>filas = []</c>. El parser
    /// de la app solo acepta coma, asi que no hay caso de uso para variarlo.
    /// </remarks>
    public static byte[] BuildCsv(params string[] filas)
    {
        if (filas.Length == 0)
            throw new ArgumentException("Se requiere al menos la fila de encabezados.", nameof(filas));

        var sb = new StringBuilder();
        for (int i = 0; i < filas.Length; i++)
        {
            sb.Append(filas[i]);
            if (i < filas.Length - 1) sb.Append('\n');
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Genera un CSV con el delimitador indicado. Usado para probar que el
    /// parser detecta ';' (default del proyecto) y ',' (fallback).
    /// </summary>
    /// <param name="separador">Caracter delimitador (';' o ','). El caller
    /// es responsable de usar el mismo caracter en todas las filas del array
    /// (incluyendo el header); este builder no hace sustituciones.</param>
    /// <param name="filas">Primera fila = header, resto = datos.</param>
    public static byte[] BuildCsvConSeparador(char separador, params string[] filas)
    {
        if (filas.Length == 0)
            throw new ArgumentException("Se requiere al menos la fila de encabezados.", nameof(filas));

        var sb = new StringBuilder();
        for (int i = 0; i < filas.Length; i++)
        {
            sb.Append(filas[i]);
            if (i < filas.Length - 1) sb.Append('\n');
        }
        _ = separador; // documentado para el lector; no transformamos el contenido
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Genera un CSV con la codificacion indicada (ej: Encoding.GetEncoding("Windows-1252")
    /// para el caso tipico de "Guardar como CSV" en Excel sobre Windows en espanol).
    /// </summary>
    public static byte[] BuildCsvConEncoding(Encoding encoding, params string[] filas)
    {
        if (filas.Length == 0)
            throw new ArgumentException("Se requiere al menos la fila de encabezados.", nameof(filas));
        if (encoding is null)
            throw new ArgumentNullException(nameof(encoding));

        var sb = new StringBuilder();
        for (int i = 0; i < filas.Length; i++)
        {
            sb.Append(filas[i]);
            if (i < filas.Length - 1) sb.Append('\n');
        }
        return encoding.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Genera un CSV UTF-8 con BOM al inicio. Util para probar que el parser
    /// maneja el byte-order-mark de UTF-8 sin lanzar excepciones.
    /// </summary>
    public static byte[] BuildCsvConBOM(params string[] filas)
    {
        var baseBytes = BuildCsv(filas);
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var result = new byte[bom.Length + baseBytes.Length];
        Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
        Buffer.BlockCopy(baseBytes, 0, result, bom.Length, baseBytes.Length);
        return result;
    }

    /// <summary>
    /// Genera un XLSX (formato OOXML) en memoria con la primera fila como
    /// encabezados y el resto como datos. Usa NPOI (mismo paquete que la app).
    /// </summary>
    /// <param name="encabezados">Nombres de las columnas (primera fila).</param>
    /// <param name="filas">Filas de datos. Cada item es un array con un valor por columna.</param>
    public static byte[] BuildXlsx(string[] encabezados, params string[][] filas)
    {
        if (encabezados.Length == 0)
            throw new ArgumentException("Se requieren encabezados.", nameof(encabezados));

        using var ms = new MemoryStream();
        using (var workbook = new XSSFWorkbook())
        {
            var sheet = workbook.CreateSheet("Datos");

            // Fila 0: encabezados
            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < encabezados.Length; i++)
                headerRow.CreateCell(i).SetCellValue(encabezados[i]);

            // Filas 1..N: datos
            for (int r = 0; r < filas.Length; r++)
            {
                var row = sheet.CreateRow(r + 1);
                var valores = filas[r];
                if (valores.Length != encabezados.Length)
                    throw new ArgumentException(
                        $"La fila {r} tiene {valores.Length} valores pero hay {encabezados.Length} encabezados.",
                        nameof(filas));

                for (int c = 0; c < valores.Length; c++)
                {
                    var cell = row.CreateCell(c);
                    // Intentamos parsear como numero para que NPOI serialice
                    // el tipo correcto al lector del ExcelFileParser.
                    if (double.TryParse(valores[c], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d))
                    {
                        cell.SetCellValue(d);
                    }
                    else
                    {
                        cell.SetCellValue(valores[c]);
                    }
                }
            }

            workbook.Write(ms, leaveOpen: true);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Genera un XLS (formato legacy BIFF, HSSF) en memoria. Util para probar
    /// que el parser de la app detecta la firma OLE2 y crea un <c>HSSFWorkbook</c>
    /// (no XSSF).
    /// </summary>
    public static byte[] BuildXls(string[] encabezados, params string[][] filas)
    {
        if (encabezados.Length == 0)
            throw new ArgumentException("Se requieren encabezados.", nameof(encabezados));

        using var ms = new MemoryStream();
        using (var workbook = new HSSFWorkbook())
        {
            var sheet = workbook.CreateSheet("Datos");

            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < encabezados.Length; i++)
                headerRow.CreateCell(i).SetCellValue(encabezados[i]);

            for (int r = 0; r < filas.Length; r++)
            {
                var row = sheet.CreateRow(r + 1);
                var valores = filas[r];
                if (valores.Length != encabezados.Length)
                    throw new ArgumentException(
                        $"La fila {r} tiene {valores.Length} valores pero hay {encabezados.Length} encabezados.",
                        nameof(filas));

                for (int c = 0; c < valores.Length; c++)
                {
                    var cell = row.CreateCell(c);
                    if (double.TryParse(valores[c], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d))
                    {
                        cell.SetCellValue(d);
                    }
                    else
                    {
                        cell.SetCellValue(valores[c]);
                    }
                }
            }

            workbook.Write(ms, leaveOpen: true);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Genera un XLSX vacio (solo fila de encabezados, sin filas de datos).
    /// Util para testear el caso "archivo sin filas".
    /// </summary>
    public static byte[] BuildXlsxSoloEncabezados(params string[] encabezados)
        => BuildXlsx(encabezados);

    /// <summary>
    /// Genera bytes dummy de un archivo .txt. Solo se usa para verificar que
    /// el FileValidator rechaza extensiones no permitidas.
    /// </summary>
    public static byte[] BuildTxt(params string[] lineas)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < lineas.Length; i++)
        {
            sb.Append(lineas[i]);
            if (i < lineas.Length - 1) sb.Append('\n');
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Genera un CSV de tamano arbitrario (mayor al limite de 10MB si se requiere)
    /// para testear el caso de tamano excedido. Cada linea es de tamano fijo.
    /// </summary>
    public static byte[] BuildCsvGrande(int cantidadFilas, int anchoFila = 200)
    {
        var sb = new StringBuilder();
        sb.Append("Codigo,Nombre\n"); // header
        for (int i = 0; i < cantidadFilas; i++)
        {
            sb.Append("B-").Append(i.ToString("D10")).Append(',');
            sb.Append('x', anchoFila);
            sb.Append('\n');
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
