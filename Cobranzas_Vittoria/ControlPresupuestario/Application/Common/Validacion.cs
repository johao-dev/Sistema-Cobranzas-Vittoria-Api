using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

/// <summary>Validaciones sintácticas de entrada compartidas por los validadores de cada feature.</summary>
internal static class Validacion
{
    public static void Id(int valor, string campo)
    {
        if (valor <= 0) throw ValidacionPresupuestariaException.IdentificadorInvalido(campo);
    }

    public static void Id(long valor, string campo)
    {
        if (valor <= 0) throw ValidacionPresupuestariaException.IdentificadorInvalido(campo);
    }

    public static void IdOpcional(int? valor, string campo)
    {
        if (valor is <= 0) throw ValidacionPresupuestariaException.IdentificadorInvalido(campo);
    }

    public static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
