namespace Cobranzas_Vittoria.Application.Common.Exports;

/// <summary>
/// Atributo que marca una propiedad de un DTO como columna exportable a Excel.
///
/// <para>
/// <b>Convención</b>: el helper genérico de exportación
/// (<see cref="IExcelExporter"/>) descubre las columnas a partir de las
/// propiedades que tengan este atributo. Las propiedades sin atributo se
/// ignoran (opt-in explicito para evitar exponer campos sensibles por error).
/// </para>
///
/// <para>
/// <b>Por que un atributo y no un mapeo por convención</b>:
///   - el orden, encabezado y formato son parte del contrato del Excel, no
///     del modelo de dominio. Un atributo explicito evita magia de
///     "nombre = Header" que rompe cuando se renombra una propiedad;
///   - permite usar nombres de columna en español sin tocar los nombres
///     de las propiedades en C# (que estan en espanol por convencion del
///     proyecto, pero pueden divergir: ej: <c>NumeroDocumento</c> ->
///     "N° Documento");
///   - facilita el test: la unidad valida que las columnas se exponen en
///     el orden y con los headers declarados.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ExcelColumnAttribute : Attribute
{
    /// <summary>Encabezado visible en la primera fila de la hoja.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>
    /// Orden de la columna en la hoja. Las propiedades se ordenan por
    /// este valor ascendente; empates conservan el orden de declaracion
    /// de la clase.
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Formato de la celda (NPOI data format). Ejemplos:
    ///   - <c>"dd/MM/yyyy"</c> para fechas.
    ///   - <c>"#,##0.00"</c> o <c>"0.00"</c> para numeros con 2 decimales.
    ///   - vacio para texto.
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Si es <c>true</c> y la columna es numerica, se incluye en la fila
    /// de totales (SUM) cuando <see cref="ExcelSheetConfig.IncludeTotalsRow"/>
    /// esta activo. Para columnas no numericas se ignora.
    /// </summary>
    public bool IncludeInTotals { get; set; } = false;

    /// <summary>
    /// Ancho de la columna en caracteres. <c>0</c> usa el ancho por
    /// defecto (15 chars para texto, 18 para numeros/fechas).
    /// </summary>
    public int Width { get; set; } = 0;
}
