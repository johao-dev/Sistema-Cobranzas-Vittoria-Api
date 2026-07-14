using System.Globalization;

namespace Cobranzas_Vittoria.Domain.Importacion;

/// <summary>
/// Representa una fila individual de un archivo de hoja de calculo (CSV o Excel)
/// ya leida y normalizada por un <c>IFileParser</c>.
///
/// Es inmutable y agnostica al formato de origen. Las celdas se almacenan como
/// <see cref="string"/> y se exponen accesores tipados (<see cref="int"/>,
/// <see cref="decimal"/>, <see cref="bool"/>, <see cref="DateTime"/>) que
/// aplican conversion con <see cref="CultureInfo.InvariantCulture"/>.
///
/// Las busquedas por nombre de columna son case-insensitive (OrdinalIgnoreCase)
/// para tolerar diferencias de capitalizacion entre el archivo y el DTO.
///
/// Cuando un accesor tipado no encuentra la columna o el valor no se puede
/// convertir, lanza <see cref="KeyNotFoundException"/> o <see cref="FormatException"/>.
/// Los processors de cada modulo son responsables de traducir estas excepciones
/// a <c>DatosInvalidosException</c> con el detalle de fila y campo correspondiente.
/// </summary>
public sealed class SpreadsheetRow
{
    /// <summary>
    /// Numero de fila en el archivo (1-based, sin contar la fila de encabezados).
    /// La primera fila de datos es 1.
    /// </summary>
    public int NumeroFila { get; }

    /// <summary>
    /// Diccionario de celdas indexado por nombre de columna.
    /// Las claves se comparan con <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Celdas { get; }

    public SpreadsheetRow(int numeroFila, IDictionary<string, string> celdas)
    {
        if (numeroFila < 1)
            throw new ArgumentOutOfRangeException(nameof(numeroFila), numeroFila, "El numero de fila debe ser >= 1.");
        if (celdas is null)
            throw new ArgumentNullException(nameof(celdas));

        NumeroFila = numeroFila;
        Celdas = new Dictionary<string, string>(celdas, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Devuelve los nombres de columna presentes en la fila.</summary>
    public IEnumerable<string> Columnas => Celdas.Keys;

    /// <summary>True si la columna existe en la fila (case-insensitive).</summary>
    public bool ContieneColumna(string columna) => Celdas.ContainsKey(columna);

    /// <summary>
    /// Obtiene el valor de la columna como string, o null si la columna no existe
    /// o el valor es vacio.
    /// </summary>
    public string? GetString(string columna)
    {
        if (Celdas.TryGetValue(columna, out var value) && !string.IsNullOrEmpty(value))
            return value;
        return null;
    }

    /// <summary>Variante Try de <see cref="GetString"/>.</summary>
    public bool TryGetString(string columna, out string? value)
    {
        if (Celdas.TryGetValue(columna, out var raw) && !string.IsNullOrEmpty(raw))
        {
            value = raw;
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>
    /// Obtiene el valor de la columna como <see cref="int"/>.
    /// Lanza <see cref="KeyNotFoundException"/> si la columna no existe,
    /// o <see cref="FormatException"/> si el valor no es un entero valido.
    /// </summary>
    public int GetInt32(string columna)
    {
        var raw = RequerirValor(columna);
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            throw new FormatException($"La columna '{columna}' de la fila {NumeroFila} no es un entero valido: '{raw}'.");
        return result;
    }

    /// <summary>Variante Try de <see cref="GetInt32"/>.</summary>
    public bool TryGetInt32(string columna, out int value)
    {
        value = 0;
        if (!Celdas.TryGetValue(columna, out var raw) || string.IsNullOrEmpty(raw))
            return false;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Obtiene el valor de la columna como <see cref="decimal"/>.
    /// Lanza <see cref="KeyNotFoundException"/> si la columna no existe,
    /// o <see cref="FormatException"/> si el valor no es un decimal valido.
    /// </summary>
    public decimal GetDecimal(string columna)
    {
        var raw = RequerirValor(columna);
        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            throw new FormatException($"La columna '{columna}' de la fila {NumeroFila} no es un decimal valido: '{raw}'.");
        return result;
    }

    /// <summary>Variante Try de <see cref="GetDecimal"/>.</summary>
    public bool TryGetDecimal(string columna, out decimal value)
    {
        value = 0m;
        if (!Celdas.TryGetValue(columna, out var raw) || string.IsNullOrEmpty(raw))
            return false;
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Obtiene el valor de la columna como <see cref="bool"/>.
    /// Acepta: "true"/"false", "1"/"0", "si"/"no" (case-insensitive).
    /// Lanza <see cref="FormatException"/> si el valor no es reconocible.
    /// </summary>
    public bool GetBool(string columna)
    {
        var raw = RequerirValor(columna);
        return ParseBool(raw, columna);
    }

    /// <summary>Variante Try de <see cref="GetBool"/>.</summary>
    public bool TryGetBool(string columna, out bool value)
    {
        value = false;
        if (!Celdas.TryGetValue(columna, out var raw) || string.IsNullOrEmpty(raw))
            return false;
        return TryParseBool(raw, out value);
    }

    /// <summary>
    /// Obtiene el valor de la columna como <see cref="DateTime"/>.
    /// Lanza <see cref="KeyNotFoundException"/> si la columna no existe,
    /// o <see cref="FormatException"/> si el valor no es una fecha valida.
    /// </summary>
    public DateTime GetDateTime(string columna)
    {
        var raw = RequerirValor(columna);
        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            throw new FormatException($"La columna '{columna}' de la fila {NumeroFila} no es una fecha valida: '{raw}'.");
        return result;
    }

    /// <summary>Variante Try de <see cref="GetDateTime"/>.</summary>
    public bool TryGetDateTime(string columna, out DateTime value)
    {
        value = default;
        if (!Celdas.TryGetValue(columna, out var raw) || string.IsNullOrEmpty(raw))
            return false;
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private string RequerirValor(string columna)
    {
        if (!Celdas.TryGetValue(columna, out var raw) || string.IsNullOrEmpty(raw))
            throw new KeyNotFoundException($"La columna '{columna}' no existe o esta vacia en la fila {NumeroFila}.");
        return raw;
    }

    private static bool ParseBool(string raw, string columna)
    {
        if (TryParseBool(raw, out var value)) return value;
        throw new FormatException($"La columna '{columna}' de la fila contiene un valor booleano invalido: '{raw}'.");
    }

    private static bool TryParseBool(string raw, out bool value)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "true" or "1" or "si" or "sí" or "yes":
                value = true;
                return true;
            case "false" or "0" or "no":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }
}
