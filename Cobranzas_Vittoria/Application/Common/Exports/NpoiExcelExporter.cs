using System.Collections.Concurrent;
using System.Reflection;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace Cobranzas_Vittoria.Application.Common.Exports;

/// <summary>
/// Implementacion de <see cref="IExcelExporter"/> con NPOI (formato <c>.xlsx</c>).
///
/// <para>
/// <b>Por que NPOI y no ClosedXML/EPPlus</b>:
/// el proyecto ya usa NPOI 2.7.1 para el modulo de Importacion
/// (<c>ExcelFileParser.cs</c>), por lo que no se agregan dependencias.
/// NPOI cubre el subconjunto de operaciones que necesitamos (estilos,
/// formatos, merges, columnas) y su API es estable.
/// </para>
///
/// <para>
/// <b>Ciclo de vida del workbook</b>:
/// cada llamada a <see cref="ExportToXlsx{T}"/> crea un <c>XSSFWorkbook</c>
/// nuevo, lo escribe a un <c>MemoryStream</c> y libera todo al
/// finalizar (<c>using</c>). Esto evita estados compartidos entre
/// exportaciones concurrentes. El costo es O(1) por exportacion y es
/// aceptable para reportes tipicos de Kardex (cientos a miles de filas).
/// </para>
///
/// <para>
/// <b>Cache de reflexion</b>:
/// las propiedades con <see cref="ExcelColumnAttribute"/> se descubren
/// una vez por tipo y se cachean en
/// <see cref="ColumnCache"/>. Asi, exportar N filas del mismo T solo
/// paga el costo de reflexion la primera vez.
/// </para>
/// </summary>
public sealed class NpoiExcelExporter : IExcelExporter
{
    private const int DefaultTextWidth = 18;
    private const int DefaultNumberWidth = 22;
    private const int DefaultDateWidth = 14;
    private const string DefaultNumberFormat = "#,##0.00";
    private const string DefaultDateFormat = "dd/MM/yyyy";

    private static readonly ConcurrentDictionary<Type, ExcelColumnInfo[]> ColumnCache = new();

    public byte[] ExportToXlsx<T>(IEnumerable<T> rows, ExcelSheetConfig config) where T : class
    {
        ArgumentNullException.ThrowIfNull(config);
        var data = rows?.ToList() ?? new List<T>();
        var columns = GetColumns(typeof(T));

        if (columns.Length == 0)
        {
            // Sin columnas que exportar: devolvemos un libro vacio pero valido
            // (es la salida minima que Excel acepta sin errores).
            using var emptyWb = new XSSFWorkbook();
            emptyWb.CreateSheet(config.SheetName);
            using var emptyMs = new MemoryStream();
            emptyWb.Write(emptyMs, leaveOpen: false);
            return emptyMs.ToArray();
        }

        using var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet(config.SheetName);

        // ----- Metadata del workbook -----
        var meta = workbook.GetProperties();
        // NPOI 2.7.1: el "author" se llama "Creator" en CoreProperties.
        meta.CoreProperties.Creator = config.Author;
        meta.CoreProperties.Title = config.Title ?? config.SheetName;
        meta.CoreProperties.Created = DateTime.Now;

        // ----- Estilos (cacheados dentro del workbook) -----
        var styles = CreateStyles(workbook);
        var customFormatCache = new Dictionary<string, ICellStyle>(StringComparer.Ordinal);

        // ----- Construccion de filas -----
        var rowIndex = 0;

        // Fila 0: margen superior
        sheet.CreateRow(rowIndex++);

        // Fila 1: titulo (si existe)
        if (!string.IsNullOrWhiteSpace(config.Title))
        {
            WriteMergedTextRow(sheet, rowIndex++, config.Title!, styles.Title, columns.Length);
        }

        // Fila 2: vacia
        sheet.CreateRow(rowIndex++);

        // Fila 3: subtitulo de filtros (si existe)
        if (!string.IsNullOrWhiteSpace(config.FiltersSubtitle))
        {
            WriteMergedTextRow(sheet, rowIndex++, config.FiltersSubtitle!, styles.Subtitle, columns.Length);
        }

        // Fila 4: "Generado el ..."
        var generatedAtText = string.Format(
            config.GeneratedAtSubtitle,
            DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
        WriteMergedTextRow(sheet, rowIndex++, generatedAtText, styles.Subtitle, columns.Length);

        // Filas vacias hasta llegar a HeaderRowIndex
        while (rowIndex < config.HeaderRowIndex)
        {
            sheet.CreateRow(rowIndex++);
        }

        // Header de columnas
        var headerRow = sheet.CreateRow(rowIndex++);
        for (var c = 0; c < columns.Length; c++)
        {
            var cell = headerRow.CreateCell(c);
            cell.SetCellValue(columns[c].Attribute.Header);
            cell.CellStyle = styles.Header;
        }

        // Filas de datos
        foreach (var item in data)
        {
            var dataRow = sheet.CreateRow(rowIndex++);
            for (var c = 0; c < columns.Length; c++)
            {
                var col = columns[c];
                var value = col.Property.GetValue(item);
                var cell = dataRow.CreateCell(c);
                ApplyCellValue(cell, value, col, styles, customFormatCache);
            }
        }

        // Fila de totales (opcional)
        if (config.IncludeTotalsRow && data.Count > 0 && columns.Any(x => x.Attribute.IncludeInTotals))
        {
            var totalsRow = sheet.CreateRow(rowIndex++);
            var firstNonTotal = -1;
            for (var c = 0; c < columns.Length; c++)
            {
                if (!columns[c].Attribute.IncludeInTotals)
                {
                    firstNonTotal = c;
                    break;
                }
            }
            for (var c = 0; c < columns.Length; c++)
            {
                var col = columns[c];
                var cell = totalsRow.CreateCell(c);
                if (col.Attribute.IncludeInTotals && col.IsNumeric)
                {
                    var sum = data.Sum(item => Convert.ToDecimal(col.Property.GetValue(item) ?? 0m));
                    cell.SetCellValue((double)sum);
                    cell.CellStyle = styles.TotalNumber;
                }
                else
                {
                    cell.SetCellValue(c == firstNonTotal ? "TOTAL" : string.Empty);
                    cell.CellStyle = styles.TotalText;
                }
            }
        }

        // Anchos de columna
        for (var c = 0; c < columns.Length; c++)
        {
            var width = columns[c].Attribute.Width > 0
                ? columns[c].Attribute.Width
                : GetDefaultWidth(columns[c]);
            sheet.SetColumnWidth(c, width * 256);
        }

        // ----- Serializacion a bytes -----
        using var ms = new MemoryStream();
        workbook.Write(ms, leaveOpen: false);
        return ms.ToArray();
    }

    // ============================================================================
    // Helpers privados
    // ============================================================================

    /// <summary>
    /// Obtiene (o calcula y cachea) la lista de columnas exportables del tipo
    /// <paramref name="type"/>, ordenadas por <see cref="ExcelColumnAttribute.Order"/>.
    /// </summary>
    private static ExcelColumnInfo[] GetColumns(Type type)
    {
        return ColumnCache.GetOrAdd(type, t =>
        {
            var props = t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetCustomAttribute<ExcelColumnAttribute>() is not null)
                .Select(p => new ExcelColumnInfo
                {
                    Property = p,
                    Attribute = p.GetCustomAttribute<ExcelColumnAttribute>()!,
                    IsNumeric = IsNumericType(p.PropertyType)
                })
                .OrderBy(x => x.Attribute.Order)
                .ThenBy(x => x.Property.MetadataToken) // estable para empates
                .ToArray();
            return props;
        });
    }

    /// <summary>
    /// Determina si un tipo es numerico a efectos de formato y totales.
    /// Incluye los nullables de cada tipo.
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(decimal) || t == typeof(double) || t == typeof(float)
            || t == typeof(int) || t == typeof(long) || t == typeof(short)
            || t == typeof(byte) || t == typeof(uint) || t == typeof(ulong)
            || t == typeof(ushort) || t == typeof(sbyte);
    }

    /// <summary>
    /// Aplica el valor y estilo apropiado a la celda segun el tipo del
    /// dato y el formato declarado en <see cref="ExcelColumnAttribute"/>.
    /// </summary>
    private static void ApplyCellValue(
        ICell cell,
        object? value,
        ExcelColumnInfo col,
        WorkbookStyles styles,
        Dictionary<string, ICellStyle> customFormatCache)
    {
        // Null: celda vacia con estilo neutro.
        if (value is null)
        {
            cell.SetCellType(CellType.Blank);
            cell.CellStyle = col.IsNumeric ? styles.DataNumber : styles.DataText;
            return;
        }

        // DateTime / DateOnly: formato de fecha.
        DateTime? asDate = value switch
        {
            DateTime dt => dt,
            DateOnly d => d.ToDateTime(TimeOnly.MinValue),
            _ => null
        };
        if (asDate.HasValue)
        {
            cell.SetCellValue(asDate.Value);
            cell.CellStyle = col.Attribute.Format.Length > 0
                ? GetOrCreateFormatStyle(styles.Workbook, customFormatCache, col.Attribute.Format, isDate: true, baseStyle: styles.DataDate)
                : styles.DataDate;
            return;
        }

        // Numerico: formato numerico.
        if (col.IsNumeric)
        {
            cell.SetCellValue(Convert.ToDouble(value));
            cell.CellStyle = col.Attribute.Format.Length > 0
                ? GetOrCreateFormatStyle(styles.Workbook, customFormatCache, col.Attribute.Format, isDate: false, baseStyle: styles.DataNumber)
                : styles.DataNumber;
            return;
        }

        // Booleano: texto SI/NO.
        if (value is bool b)
        {
            cell.SetCellValue(b ? "SI" : "NO");
            cell.CellStyle = styles.DataText;
            return;
        }

        // Default: texto.
        cell.SetCellValue(value.ToString() ?? string.Empty);
        cell.CellStyle = styles.DataText;
    }

    /// <summary>
    /// Crea (o recupera del cache) un estilo que reutiliza la apariencia
    /// de <paramref name="baseStyle"/> pero con un formato numerico/de fecha
    /// especifico. Asi no se rompe la coherencia visual cuando una columna
    /// define un formato custom.
    /// </summary>
    private static ICellStyle GetOrCreateFormatStyle(
        IWorkbook workbook,
        Dictionary<string, ICellStyle> cache,
        string format,
        bool isDate,
        ICellStyle baseStyle)
    {
        if (cache.TryGetValue(format, out var cached))
        {
            return cached;
        }

        var style = workbook.CreateCellStyle();
        style.CloneStyleFrom(baseStyle);
        var dataFormat = workbook.CreateDataFormat();
        style.DataFormat = dataFormat.GetFormat(isDate ? format : (format.Length > 0 ? format : DefaultNumberFormat));
        cache[format] = style;
        return style;
    }

    /// <summary>Ancho por defecto (en chars) segun el tipo de la columna.</summary>
    private static int GetDefaultWidth(ExcelColumnInfo col)
    {
        if (col.Property.PropertyType == typeof(DateTime) || col.Property.PropertyType == typeof(DateOnly))
        {
            return DefaultDateWidth;
        }
        return col.IsNumeric ? DefaultNumberWidth : DefaultTextWidth;
    }

    /// <summary>
    /// Escribe una fila de una sola celda (merged sobre todas las columnas)
    /// con el texto y estilo indicados. Si hay una sola columna no agrega
    /// merge region (NPOI lo permite pero es innecesario).
    /// </summary>
    private static void WriteMergedTextRow(ISheet sheet, int rowIndex, string text, ICellStyle style, int columnCount)
    {
        var row = sheet.CreateRow(rowIndex);
        var cell = row.CreateCell(0);
        cell.SetCellValue(text);
        cell.CellStyle = style;
        if (columnCount > 1)
        {
            sheet.AddMergedRegion(new CellRangeAddress(rowIndex, rowIndex, 0, columnCount - 1));
        }
    }

    /// <summary>
    /// Construye todos los estilos de la hoja. Los estilos se crean aqui y
    /// se reusan durante toda la exportacion. Crearlos por celda seria
    /// O(n) en lugar de O(1) y produciria un .xlsx inflado.
    /// </summary>
    private static WorkbookStyles CreateStyles(IWorkbook workbook)
    {
        // ---- Fuentes ----
        var titleFont = workbook.CreateFont();
        titleFont.IsBold = true;
        titleFont.FontHeightInPoints = 14;
        titleFont.Color = NPOI.SS.UserModel.IndexedColors.Black.Index;

        var subtitleFont = workbook.CreateFont();
        subtitleFont.IsItalic = true;
        subtitleFont.FontHeightInPoints = 10;
        subtitleFont.Color = NPOI.SS.UserModel.IndexedColors.Grey50Percent.Index;

        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerFont.FontHeightInPoints = 11;
        headerFont.Color = NPOI.SS.UserModel.IndexedColors.White.Index;

        var dataFont = workbook.CreateFont();
        dataFont.FontHeightInPoints = 10;

        var totalFont = workbook.CreateFont();
        totalFont.IsBold = true;
        totalFont.FontHeightInPoints = 11;

        // ---- Colores de fondo ----
        var headerBg = new XSSFColor(new byte[] { 0x1F, 0x4E, 0x79 }); // azul oscuro
        var totalBg = new XSSFColor(new byte[] { 0xF2, 0xF2, 0xF2 }); // gris claro

        var dataFormat = workbook.CreateDataFormat();

        // ---- Title ----
        var title = workbook.CreateCellStyle();
        title.SetFont(titleFont);
        title.Alignment = HorizontalAlignment.Center;
        title.VerticalAlignment = VerticalAlignment.Center;

        // ---- Subtitle ----
        var subtitle = workbook.CreateCellStyle();
        subtitle.SetFont(subtitleFont);
        subtitle.Alignment = HorizontalAlignment.Left;
        subtitle.VerticalAlignment = VerticalAlignment.Center;

        // ---- Header ----
        var header = workbook.CreateCellStyle();
        header.SetFont(headerFont);
        header.Alignment = HorizontalAlignment.Center;
        header.VerticalAlignment = VerticalAlignment.Center;
        // NPOI 2.7.1: la propiedad de la interfaz ICellStyle es `short`; para
        // asignar un XSSFColor (RGB) hay que castear a XSSFCellStyle y usar
        // SetFillForegroundColor(XSSFColor). Lo mismo aplica a TotalText/TotalNumber.
        ((XSSFCellStyle)header).SetFillForegroundColor(headerBg);
        header.FillPattern = FillPattern.SolidForeground;
        SetThinBorder(header);

        // ---- Data text ----
        var dataText = workbook.CreateCellStyle();
        dataText.SetFont(dataFont);
        dataText.Alignment = HorizontalAlignment.Left;
        dataText.VerticalAlignment = VerticalAlignment.Center;
        dataText.WrapText = false;
        SetThinBorder(dataText);

        // ---- Data number ----
        var dataNumber = workbook.CreateCellStyle();
        dataNumber.SetFont(dataFont);
        dataNumber.Alignment = HorizontalAlignment.Right;
        dataNumber.VerticalAlignment = VerticalAlignment.Center;
        dataNumber.DataFormat = dataFormat.GetFormat(DefaultNumberFormat);
        SetThinBorder(dataNumber);

        // ---- Data date ----
        var dataDate = workbook.CreateCellStyle();
        dataDate.SetFont(dataFont);
        dataDate.Alignment = HorizontalAlignment.Center;
        dataDate.VerticalAlignment = VerticalAlignment.Center;
        dataDate.DataFormat = dataFormat.GetFormat(DefaultDateFormat);
        SetThinBorder(dataDate);

        // ---- Total text ----
        var totalText = workbook.CreateCellStyle();
        totalText.SetFont(totalFont);
        totalText.Alignment = HorizontalAlignment.Left;
        totalText.VerticalAlignment = VerticalAlignment.Center;
        ((XSSFCellStyle)totalText).SetFillForegroundColor(totalBg);
        totalText.FillPattern = FillPattern.SolidForeground;
        SetThinBorder(totalText);

        // ---- Total number ----
        var totalNumber = workbook.CreateCellStyle();
        totalNumber.SetFont(totalFont);
        totalNumber.Alignment = HorizontalAlignment.Right;
        totalNumber.VerticalAlignment = VerticalAlignment.Center;
        totalNumber.DataFormat = dataFormat.GetFormat(DefaultNumberFormat);
        ((XSSFCellStyle)totalNumber).SetFillForegroundColor(totalBg);
        totalNumber.FillPattern = FillPattern.SolidForeground;
        SetThinBorder(totalNumber);

        return new WorkbookStyles(
            Workbook: workbook,
            Title: title,
            Subtitle: subtitle,
            Header: header,
            DataText: dataText,
            DataNumber: dataNumber,
            DataDate: dataDate,
            TotalText: totalText,
            TotalNumber: totalNumber);
    }

    private static void SetThinBorder(ICellStyle style)
    {
        style.BorderTop = BorderStyle.Thin;
        style.BorderBottom = BorderStyle.Thin;
        style.BorderLeft = BorderStyle.Thin;
        style.BorderRight = BorderStyle.Thin;
    }

    // ============================================================================
    // Tipos internos
    // ============================================================================

    /// <summary>Snapshot inmutable de la metadata de una columna exportable.</summary>
    private sealed class ExcelColumnInfo
    {
        public PropertyInfo Property { get; init; } = null!;
        public ExcelColumnAttribute Attribute { get; init; } = null!;
        public bool IsNumeric { get; init; }
    }

    /// <summary>Bundle de estilos + referencia al workbook para crear formatos custom.</summary>
    private sealed record WorkbookStyles(
        IWorkbook Workbook,
        ICellStyle Title,
        ICellStyle Subtitle,
        ICellStyle Header,
        ICellStyle DataText,
        ICellStyle DataNumber,
        ICellStyle DataDate,
        ICellStyle TotalText,
        ICellStyle TotalNumber);
}
