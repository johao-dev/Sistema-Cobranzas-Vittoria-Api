using System.Text;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Http;

/// <summary>Plantillas de importación en CSV (';' y BOM para que Excel respete las tildes) o XLSX.</summary>
public static class PlantillaArchivo
{
    public const string TipoCsv = "text/csv; charset=utf-8";
    public const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static string NormalizarFormato(string? formato)
    {
        var normalizado = (formato ?? "csv").Trim().ToLowerInvariant();
        return normalizado is "csv" or "xlsx"
            ? normalizado
            : throw new FormatoPlantillaInvalidoException(formato ?? string.Empty,
                $"El formato '{formato}' no es valido. Use 'csv' o 'xlsx'.");
    }

    public static byte[] Csv(IEnumerable<string> encabezados, IEnumerable<IEnumerable<string>>? filas = null)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(';', encabezados.Select(Celda))).Append("\r\n");
        foreach (var fila in filas ?? Enumerable.Empty<IEnumerable<string>>())
            sb.Append(string.Join(';', fila.Select(Celda))).Append("\r\n");
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return encoding.GetPreamble().Concat(encoding.GetBytes(sb.ToString())).ToArray();
    }

    public static string NombreArchivo(string prefijo, string formato) => $"{prefijo}-{DateTime.Now:yyyyMMdd-HHmm}.{formato}";

    private static string Celda(string valor)
        => valor.IndexOfAny(new[] { ';', '"', '\r', '\n' }) >= 0 ? "\"" + valor.Replace("\"", "\"\"") + "\"" : valor;
}
