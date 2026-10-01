using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

/// <summary>Estado de una versión de presupuesto (catálogo EstadoPresupuesto).</summary>
public sealed record EstadoPresupuesto(int IdEstadoPresupuesto, string Codigo, string Nombre, string? Descripcion, bool Activo)
{
    public const string Borrador = "BORRADOR";
    public const string Aprobado = "APROBADO";
    public const string Historico = "HISTORICO";
    public const string Anulado = "ANULADO";

    private static readonly HashSet<string> Codigos = new(StringComparer.Ordinal) { Borrador, Aprobado, Historico, Anulado };

    /// <summary>Normaliza un código escrito por el usuario; lanza 400 si no es un estado conocido.</summary>
    public static string NormalizarCodigo(string codigo)
    {
        var normalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (!Codigos.Contains(normalizado))
            throw new ValidacionPresupuestariaException("ESTADO_INVALIDO",
                "EstadoPresupuesto debe ser BORRADOR, APROBADO, HISTORICO o ANULADO.");
        return normalizado;
    }
}
