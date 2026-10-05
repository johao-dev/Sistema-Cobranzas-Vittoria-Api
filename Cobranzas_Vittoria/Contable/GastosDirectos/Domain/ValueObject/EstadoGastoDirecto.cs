using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Domain.ValueObject;

public static class EstadoGastoDirecto
{
    public const string Registrado = "REGISTRADO";
    public const string Confirmado = "CONFIRMADO";
    public const string Anulado = "ANULADO";

    private static readonly HashSet<string> Estados = new(StringComparer.Ordinal) { Registrado, Confirmado, Anulado };

    public static string? Normalizar(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado)) return null;
        var normalizado = estado.Trim().ToUpperInvariant();
        return Estados.Contains(normalizado) ? normalizado
            : throw new ValidacionGastoDirectoException("Estado debe ser REGISTRADO, CONFIRMADO o ANULADO.");
    }
}
