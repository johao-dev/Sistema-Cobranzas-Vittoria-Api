using System.Globalization;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Importar;

/// <summary>Lectura de montos escritos a mano en CSV/XLSX, con punto o coma decimal.</summary>
public static class MontoImportado
{
    /// <summary>
    /// Si aparecen punto y coma, el último es el decimal ("15,000.50" y "15.000,50"). Con una sola coma
    /// seguida de 1 o 2 dígitos se toma como decimal ("15000,50"); con 3 dígitos, como miles ("15,000").
    /// </summary>
    public static bool TryLeer(string texto, out decimal monto)
    {
        monto = 0;
        var s = texto.Replace(" ", string.Empty).Replace("S/", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (s.Length == 0) return false;
        var ultimoPunto = s.LastIndexOf('.');
        var ultimaComa = s.LastIndexOf(',');
        if (ultimoPunto >= 0 && ultimaComa >= 0)
        {
            var decimalEsComa = ultimaComa > ultimoPunto;
            s = decimalEsComa
                ? s.Replace(".", string.Empty).Replace(',', '.')
                : s.Replace(",", string.Empty);
        }
        else if (ultimaComa >= 0)
        {
            var comas = s.Count(c => c == ',');
            var decimales = s.Length - ultimaComa - 1;
            s = comas == 1 && decimales is 1 or 2
                ? s.Replace(',', '.')
                : s.Replace(",", string.Empty);
        }
        else if (s.Count(c => c == '.') > 1)
        {
            // "1.250.000" → miles con punto.
            s = s.Replace(".", string.Empty);
        }
        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out monto);
    }
}
