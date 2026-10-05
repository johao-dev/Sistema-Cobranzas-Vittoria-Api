using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Domain.ValueObject;

/// <summary>Sección de Operaciones → Gastos del proyecto desde la que se registra un gasto directo.</summary>
public static class SeccionGasto
{
    private static readonly HashSet<string> Codigos =
        new(StringComparer.Ordinal) { "ADMINISTRATIVO", "TERRENO", "MARKETING_VENTAS", "OTROS", "MUNICIPAL" };

    /// <summary>Devuelve el código normalizado, o null si no es requerida y no viene.</summary>
    public static string? Normalizar(string? seccion, bool requerida)
    {
        if (string.IsNullOrWhiteSpace(seccion))
            return requerida ? throw new ValidacionGastoDirectoException("Indica la sección de gasto.") : null;
        var codigo = seccion.Trim().ToUpperInvariant();
        return Codigos.Contains(codigo) ? codigo
            : throw new ValidacionGastoDirectoException(
                "La sección debe ser ADMINISTRATIVO, TERRENO, MARKETING_VENTAS, OTROS o MUNICIPAL.");
    }
}
