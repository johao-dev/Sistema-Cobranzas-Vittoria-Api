using Cobranzas_Vittoria.Application.Common.Exports;

namespace Cobranzas_Vittoria.Application.Importacion.Dtos;

/// <summary>
/// DTO de UNA fila de la plantilla de importacion del modulo <c>material</c>.
///
/// <para>
/// Esta clase no se usa para cargar datos reales al sistema (la importacion
/// usa <see cref="MaterialImportTvpDto"/>). Solo modela las COLUMNAS que el
/// archivo plantilla debe tener, para que el helper generico
/// <see cref="Application.Common.Exports.IExcelExporter"/> pueda escribir el
/// header en la primera fila de la hoja.
/// </para>
///
/// <para>
/// Los nombres de las propiedades estan en espanol por convencion del
/// proyecto y porque el helper los usa para detectar las columnas. El HEADER
/// visible en el archivo es el del <see cref="ExcelColumnAttribute.Header"/>
/// (que en este caso coincide con el nombre de la propiedad).
/// </para>
///
/// <para>
/// <b>Importante</b>: el orden de las propiedades en este archivo define el
/// orden de las columnas en la plantilla y debe coincidir con el orden
/// declarado en <see cref="MaterialImportProcessor"/>.EncabezadosRequeridos.
/// </b>
/// </para>
/// </summary>
public sealed class MaterialImportPlantillaRow
{
    [ExcelColumn(Header = "Especialidad",  Order = 1, Width = 22)]
    public string Especialidad { get; set; } = string.Empty;

    [ExcelColumn(Header = "Nombre",        Order = 2, Width = 35)]
    public string Nombre { get; set; } = string.Empty;

    [ExcelColumn(Header = "UnidadMedida",  Order = 3, Width = 18)]
    public string UnidadMedida { get; set; } = string.Empty;

    [ExcelColumn(Header = "Codigo",        Order = 4, Width = 18)]
    public string Codigo { get; set; } = string.Empty;
}
