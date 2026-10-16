using Cobranzas_Vittoria.Compras.Domain.Excepciones;

namespace Cobranzas_Vittoria.Compras.Domain.Model;

/// <summary>Validaciones compartidas para evitar contratos distintos entre cabecera e ítems.</summary>
internal static class ReglasCompras
{
    public static int Id(int valor, string campo)
        => valor > 0 ? valor : throw ValidacionComprasException.IdentificadorInvalido(campo);

    public static int? IdOpcional(int? valor, string campo)
        => valor is null or > 0 ? valor : throw ValidacionComprasException.IdentificadorInvalido(campo);

    public static string TextoRequerido(string? valor, string campo, int maximo)
    {
        string? texto = TextoOpcional(valor, campo, maximo);

        return texto ?? throw ValidacionComprasException.CampoRequerido(campo);
    }

    public static string? TextoOpcional(string? valor, string campo, int maximo)
    {
        // Se normalizan entradas nuevas; la reconstrucción no modifica textos históricos.
        string? texto = valor?.Trim();

        if (string.IsNullOrEmpty(texto))
            return null;

        if (texto.Length > maximo)
            throw ValidacionComprasException.Longitud(campo, maximo);

        return texto;
    }

    public static decimal Cantidad(decimal cantidad)
    {
        if (cantidad <= 0)
            throw new ValidacionComprasException("REQUERIMIENTO_CANTIDAD_INVALIDA",
                "La cantidad del ítem debe ser mayor que cero.");

        // SQL y los TVP usan DECIMAL(18,2): rechazar evita solicitar otra cantidad por redondeo implícito.
        if (cantidad > 9999999999999999.99m || cantidad != decimal.Round(cantidad, 2))
            throw new ValidacionComprasException("COMPRAS_PRECISION_INVALIDA",
                "La cantidad debe ser representable como DECIMAL(18,2), sin redondear.");

        return cantidad;
    }
}
