using System.Globalization;
using System.Text;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Domain.Importacion;

namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>Registro de un catálogo que el usuario puede escribir por código o por nombre.</summary>
internal sealed class ReferenciaCatalogo
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
}

/// <summary>
/// Utilidades compartidas por las importaciones de maestros de Control Presupuestario:
/// lectura de celdas acumulando errores y resolución de catálogos por código o nombre.
/// </summary>
internal static class LecturaCatalogo
{
    /// <summary>Lee y recorta una celda; registra el error (obligatoria o demasiado larga) sin cortar el proceso.</summary>
    public static string? Leer(SpreadsheetRow fila, string columna, int maximo, bool requerido,
        List<DetalleErrorFila> errores)
    {
        var valor = fila.ContieneColumna(columna) ? fila.GetString(columna)?.Trim() : null;
        if (string.IsNullOrEmpty(valor))
        {
            if (requerido)
                errores.Add(new DetalleErrorFila(fila.NumeroFila, columna, CodigosError.Fila.CampoRequerido,
                    $"La columna {columna} es obligatoria."));
            return null;
        }
        if (valor.Length > maximo)
        {
            errores.Add(new DetalleErrorFila(fila.NumeroFila, columna, CodigosError.Fila.FormatoInvalido,
                $"La columna {columna} admite como máximo {maximo} caracteres."));
            return null;
        }
        return valor;
    }

    /// <summary>Busca por código o por nombre, sin distinguir mayúsculas, tildes ni espacios/guiones bajos.</summary>
    public static ReferenciaCatalogo? Resolver(IEnumerable<ReferenciaCatalogo> catalogo, string valor)
    {
        var clave = Clave(valor);
        return catalogo.FirstOrDefault(r => Clave(r.Codigo) == clave || Clave(r.Nombre) == clave);
    }

    public static string Clave(string valor)
    {
        var sinTildes = new StringBuilder();
        foreach (var c in valor.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sinTildes.Append(c is ' ' or '-' ? '_' : char.ToUpperInvariant(c));
        }
        return sinTildes.ToString();
    }

    /// <summary>Códigos activos del catálogo, para sugerirlos en el mensaje de error.</summary>
    public static string Listar(IEnumerable<ReferenciaCatalogo> catalogo)
        => string.Join(", ", catalogo.Where(r => r.Activo).Select(r => r.Codigo).OrderBy(c => c));
}
