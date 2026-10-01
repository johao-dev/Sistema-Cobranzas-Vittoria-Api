using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Domain.ValueObject;

/// <summary>Tipo de documento adjunto a un gasto directo: Factura o Pago (solo PDF).</summary>
public static class TipoDocumentoGasto
{
    public const string Factura = "Factura";
    public const string Pago = "Pago";

    public static string Normalizar(string? tipo)
        => string.Equals(tipo, Pago, StringComparison.OrdinalIgnoreCase) ? Pago
            : string.Equals(tipo, Factura, StringComparison.OrdinalIgnoreCase) ? Factura
            : throw new ValidacionGastoDirectoException("TipoDocumento debe ser Factura o Pago.");
}
