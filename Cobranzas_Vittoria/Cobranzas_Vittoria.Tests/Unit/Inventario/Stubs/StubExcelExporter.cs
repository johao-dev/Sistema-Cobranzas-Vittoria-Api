using Cobranzas_Vittoria.Application.Common.Exports;

namespace Cobranzas_Vittoria.Tests.Unit.Inventario.Stubs;

/// <summary>
/// Stub de <see cref="IExcelExporter"/> para tests unit que no verifican
/// el contenido del Excel. Devuelve un array vacio en vez de construir
/// un workbook real (los tests que validan el formato del Excel usan
/// integration tests con el exporter real).
/// </summary>
internal sealed class StubExcelExporter : IExcelExporter
{
    public int Llamadas { get; private set; }

    public byte[] ExportToXlsx<T>(IEnumerable<T> rows, ExcelSheetConfig config) where T : class
    {
        Llamadas++;
        return Array.Empty<byte>();
    }
}
