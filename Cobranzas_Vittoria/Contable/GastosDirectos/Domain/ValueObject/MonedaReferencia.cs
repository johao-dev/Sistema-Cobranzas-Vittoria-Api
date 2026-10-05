using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Domain.ValueObject;

/// <summary>
/// Moneda original de una factura en otra moneda. Es solo referencia: el gasto se descuenta del
/// presupuesto en su moneda y este dato nunca se usa para convertir. Va completa o no va.
/// </summary>
public sealed record MonedaReferencia(int IdMonedaOriginal, decimal MontoOriginal, decimal TipoCambio, DateTime? FechaTipoCambio)
{
    public static MonedaReferencia? Crear(int idMonedaPresupuesto, int? idMonedaOriginal, decimal? montoOriginal,
        decimal? tipoCambio, DateTime? fechaTipoCambio)
    {
        var informados = new object?[] { idMonedaOriginal, montoOriginal, tipoCambio }.Count(x => x is not null);
        if (informados is not (0 or 3))
            throw new ValidacionGastoDirectoException(
                "Para registrar la factura en otra moneda indica la moneda, el monto original y el tipo de cambio.");
        if (idMonedaOriginal is null)
        {
            if (fechaTipoCambio is not null)
                throw new ValidacionGastoDirectoException("La fecha del tipo de cambio solo aplica a una factura en otra moneda.");
            return null;
        }
        if (idMonedaOriginal == idMonedaPresupuesto)
            throw new ValidacionGastoDirectoException("La moneda original debe ser distinta a la del presupuesto.");
        return new MonedaReferencia(idMonedaOriginal.Value,
            decimal.Round(montoOriginal!.Value, 2, MidpointRounding.AwayFromZero), tipoCambio!.Value, fechaTipoCambio?.Date);
    }
}
