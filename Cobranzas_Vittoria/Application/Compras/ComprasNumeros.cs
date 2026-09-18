using Cobranzas_Vittoria.Application.Compras.Excepciones;

namespace Cobranzas_Vittoria.Application.Compras;

/// <summary>Evita redondeos implícitos al enviar números a columnas y TVPs DECIMAL(18,2).</summary>
internal static class ComprasNumeros
{
    private const decimal Maximo = 9999999999999999.99m;

    public static void ValidarDecimal(decimal valor, string campo)
    {
        if (valor > Maximo || valor < -Maximo || valor != decimal.Round(valor, 2))
            throw new ValidacionNegocioComprasException(campo, "COMPRAS_PRECISION_INVALIDA",
                "El valor debe ser representable como DECIMAL(18,2), sin redondear.");
    }

    public static void ValidarMateriales(IEnumerable<int> materiales)
    {
        if (materiales.GroupBy(id => id).Any(grupo => grupo.Count() > 1))
            throw new ValidacionNegocioComprasException("items", "COMPRAS_MATERIAL_DUPLICADO",
                "Cada material puede aparecer una sola vez dentro del documento.");
    }

    public static void ValidarImportes(IEnumerable<(decimal Cantidad, decimal PrecioUnitario)> items)
    {
        decimal total = 0;
        foreach (var item in items)
        {
            ValidarDecimal(item.Cantidad, "cantidad");
            ValidarDecimal(item.PrecioUnitario, "precioUnitario");
            // Ambos factores ya caben en 18 dígitos; comprobar producto antes de multiplicar
            // evita un overflow de System.Decimal para datos extremos.
            if (item.PrecioUnitario != 0 && Math.Abs(item.Cantidad) > Maximo / Math.Abs(item.PrecioUnitario))
                throw new ValidacionNegocioComprasException("subtotal", "COMPRAS_PRECISION_INVALIDA",
                    "El subtotal excede la precisión monetaria permitida.");
            var subtotal = item.Cantidad * item.PrecioUnitario;
            total += subtotal;
            ValidarDecimal(decimal.Round(total, 2), "total");
        }
    }
}
