using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

/// <summary>Dimensión que corrige un ajuste manual: COMPROMISO o EJECUCION.</summary>
public sealed record AfectacionPresupuestal
{
    public static readonly AfectacionPresupuestal Compromiso = new("COMPROMISO");
    public static readonly AfectacionPresupuestal Ejecucion = new("EJECUCION");

    public string Valor { get; }

    private AfectacionPresupuestal(string valor) => Valor = valor;

    public static AfectacionPresupuestal Crear(string? valor)
        => (valor ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "COMPROMISO" => Compromiso,
            "EJECUCION" => Ejecucion,
            _ => throw new ValidacionPresupuestariaException("AFECTACION_INVALIDA", "Afectacion debe ser COMPROMISO o EJECUCION.")
        };

    public override string ToString() => Valor;
}
