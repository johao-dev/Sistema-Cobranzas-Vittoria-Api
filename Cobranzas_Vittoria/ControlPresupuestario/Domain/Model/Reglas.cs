using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>Reglas de forma compartidas por los modelos del módulo.</summary>
internal static class Reglas
{
    public static string Requerido(string? valor, string campo, int maximo)
    {
        var limpio = valor?.Trim();
        if (string.IsNullOrEmpty(limpio)) throw ValidacionPresupuestariaException.CampoRequerido(campo);
        if (limpio.Length > maximo) throw ValidacionPresupuestariaException.Longitud(campo, maximo);
        return limpio;
    }

    public static string? Opcional(string? valor, string campo, int maximo)
    {
        var limpio = valor?.Trim();
        if (string.IsNullOrEmpty(limpio)) return null;
        if (limpio.Length > maximo) throw ValidacionPresupuestariaException.Longitud(campo, maximo);
        return limpio;
    }

    public static int Id(int valor, string campo)
        => valor > 0 ? valor : throw ValidacionPresupuestariaException.IdentificadorInvalido(campo);

    public static int? IdOpcional(int? valor, string campo)
        => valor is null or > 0 ? valor : throw ValidacionPresupuestariaException.IdentificadorInvalido(campo);

    /// <summary>Montos en la moneda del presupuesto: no negativos y redondeados a 2 decimales.</summary>
    public static decimal Monto(decimal valor, string campo)
    {
        if (valor < 0) throw new ValidacionPresupuestariaException("MONTO_INVALIDO", $"{campo} no puede ser negativo.");
        return decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
