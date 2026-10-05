using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

/// <summary>Sentido de un ajuste manual: INCREMENTO o DECREMENTO.</summary>
public sealed record DireccionMovimiento
{
    public static readonly DireccionMovimiento Incremento = new("INCREMENTO");
    public static readonly DireccionMovimiento Decremento = new("DECREMENTO");

    public string Valor { get; }

    private DireccionMovimiento(string valor) => Valor = valor;

    public static DireccionMovimiento Crear(string? valor)
        => (valor ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "INCREMENTO" => Incremento,
            "DECREMENTO" => Decremento,
            _ => throw new ValidacionPresupuestariaException("DIRECCION_INVALIDA", "Direccion debe ser INCREMENTO o DECREMENTO.")
        };

    public override string ToString() => Valor;
}
