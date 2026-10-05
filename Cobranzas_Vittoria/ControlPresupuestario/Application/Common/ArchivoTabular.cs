namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

/// <summary>Archivo CSV/XLSX recibido por un caso de uso, independiente de ASP.NET.</summary>
public sealed record ArchivoTabular(string NombreArchivo, string? ContentType, byte[] Contenido);

/// <summary>Fila de datos de un archivo tabular. NumeroFila es la fila de datos, sin contar el encabezado.</summary>
public sealed class FilaTabular
{
    private readonly IReadOnlyDictionary<string, string?> _celdas;

    public int NumeroFila { get; }
    public IEnumerable<string> Columnas => _celdas.Keys;

    public FilaTabular(int numeroFila, IReadOnlyDictionary<string, string?> celdas)
    {
        NumeroFila = numeroFila;
        _celdas = new Dictionary<string, string?>(celdas, StringComparer.OrdinalIgnoreCase);
    }

    public bool TieneColumna(string columna) => _celdas.ContainsKey(columna);

    public string? Valor(string columna) => _celdas.TryGetValue(columna, out var valor) ? valor?.Trim() : null;
}
