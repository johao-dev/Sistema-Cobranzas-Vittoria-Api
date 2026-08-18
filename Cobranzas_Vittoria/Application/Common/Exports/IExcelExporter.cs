namespace Cobranzas_Vittoria.Application.Common.Exports;

/// <summary>
/// Contrato generico para exportar una coleccion de DTOs a un archivo
/// Excel en memoria (byte[]), listo para ser devuelto por un endpoint.
///
/// <para>
/// <b>Por que una interfaz y no una clase estatica</b>:
/// la implementacion con NPOI mantiene un workbook XSSF en memoria y
/// requiere ser instanciada. Inyectarla como interfaz en el service
/// permite sustituirla por un mock en tests unit sin tocar NPOI, y
/// dejarla como <c>Scoped</c> en el contenedor de DI es consistente
/// con el resto del proyecto (no requiere <c>IDisposable</c> manual
/// porque el workbook se libera al finalizar el metodo).
/// </para>
///
/// <para>
/// <b>Por que devuelve <c>byte[]</c> y no un <c>Stream</c></b>:
/// el controller de ASP.NET Core acepta <c>byte[]</c> directamente
/// en <c>File(bytes, contentType, fileName)</c>, que setea los headers
/// de descarga correctamente. Un <c>Stream</c> obligaria a que el
/// llamador controle la disposicion; aqui la operacion es sincronica
/// de principio a fin.
/// </para>
/// </summary>
public interface IExcelExporter
{
    /// <summary>
    /// Exporta la coleccion <paramref name="rows"/> a un libro Excel
    /// (formato <c>.xlsx</c>) segun la configuracion indicada.
    /// </summary>
    /// <typeparam name="T">
    /// Tipo de las filas. Solo se exportan las propiedades marcadas
    /// con <see cref="ExcelColumnAttribute"/>; el resto se ignoran.
    /// </typeparam>
    /// <param name="rows">Filas a exportar. Si es null o vacio, se
    /// genera un libro con solo el header.</param>
    /// <param name="config">Configuracion de la hoja (titulo, subtitulos,
    /// totales, etc.).</param>
    /// <returns>Bytes del archivo <c>.xlsx</c>.</returns>
    byte[] ExportToXlsx<T>(IEnumerable<T> rows, ExcelSheetConfig config) where T : class;
}
