namespace Cobranzas_Vittoria.Application.Common.Exports;

/// <summary>
/// Configuracion de la hoja Excel que produce <see cref="IExcelExporter"/>.
/// Cada reporte instancia esta clase con sus particularidades; la
/// implementacion con NPOI (<see cref="NpoiExcelExporter"/>) la consume
/// para escribir titulo, subtitulos, fila de totales y metadata.
///
/// <para>
/// <b>Estructura tipica de la hoja</b> (filas 0-based, ajustables):
/// <code>
///   0: (vacia, margen superior)
///   1: Title                        (merged, bold, 14pt)
///   2: (vacia)
///   3: FiltersSubtitle              (italic, gris)
///   4: "Generado el: ..."           (italic, gris)
///   5: (vacia)
///   6: HEADER (N° | Proyecto | ...)
///   7..N: datos
///   N+1 (opcional): fila de TOTALES (si IncludeTotalsRow = true)
/// </code>
/// </para>
/// </summary>
public sealed class ExcelSheetConfig
{
    /// <summary>Nombre de la hoja dentro del libro (tab inferior).</summary>
    public string SheetName { get; set; } = "Datos";

    /// <summary>
    /// Titulo principal centrado en la fila 1 (merged sobre todas las
    /// columnas). Si es null o vacio, no se escribe.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Subtítulo de filtros aplicados, fila 3 (merged). Aparece justo
    /// debajo del titulo. Si es null o vacio, no se escribe.
    /// <para>Ejemplo: "Filtros: idEspecialidad=2, idProyecto=10, fecha=2026-01-01..2026-12-31".</para>
    /// </summary>
    public string? FiltersSubtitle { get; set; }

    /// <summary>
    /// Plantilla para la fila de "generado el" (fila 4). Acepta un
    /// <c>{0}</c> que se reemplaza por la fecha actual en formato
    /// <c>dd/MM/yyyy HH:mm:ss</c>. Default: <c>"Generado el: {0}"</c>.
    /// </summary>
    public string GeneratedAtSubtitle { get; set; } = "Generado el: {0}";

    /// <summary>
    /// Si es <c>true</c>, agrega una fila final con la suma de las
    /// columnas marcadas con
    /// <see cref="ExcelColumnAttribute.IncludeInTotals"/>.
    /// Solo se agrega si hay al menos una fila de datos.
    /// </summary>
    public bool IncludeTotalsRow { get; set; } = false;

    /// <summary>Autor que se registra en las propiedades del workbook.</summary>
    public string Author { get; set; } = "Sistema de Cobranzas Vittoria";

    /// <summary>
    /// Indice (0-based) de la fila donde va el header de columnas.
    /// Default <c>6</c> deja 5 filas para titulo + subtitulos + vacias.
    /// </summary>
    public int HeaderRowIndex { get; set; } = 6;
}
