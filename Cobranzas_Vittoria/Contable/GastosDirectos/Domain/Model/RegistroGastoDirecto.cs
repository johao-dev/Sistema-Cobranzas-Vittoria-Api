using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.ValueObject;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;

/// <summary>
/// Datos para registrar o editar un gasto directo. Las reglas económicas se validan en SQL Server
/// en la misma transacción que corresponda (THROW 515xx).
/// </summary>
public sealed class RegistroGastoDirecto
{
    public int IdPresupuestoDetalle { get; }
    public int? IdProveedor { get; }
    public int IdMoneda { get; }
    public DateTime Fecha { get; }
    public string Concepto { get; }
    public string? Descripcion { get; }
    public decimal Monto { get; }
    public MonedaReferencia? MonedaReferencia { get; }

    private RegistroGastoDirecto(int idPresupuestoDetalle, int? idProveedor, int idMoneda, DateTime fecha, string concepto,
        string? descripcion, decimal monto, MonedaReferencia? monedaReferencia)
    {
        IdPresupuestoDetalle = idPresupuestoDetalle;
        IdProveedor = idProveedor;
        IdMoneda = idMoneda;
        Fecha = fecha;
        Concepto = concepto;
        Descripcion = descripcion;
        Monto = monto;
        MonedaReferencia = monedaReferencia;
    }

    public static RegistroGastoDirecto Crear(int idPresupuestoDetalle, int? idProveedor, int idMoneda, DateTime fecha,
        string? concepto, string? descripcion, decimal monto, int? idMonedaOriginal, decimal? montoOriginal,
        decimal? tipoCambio, DateTime? fechaTipoCambio)
    {
        var referencia = MonedaReferencia.Crear(idMoneda, idMonedaOriginal, montoOriginal, tipoCambio, fechaTipoCambio);
        return new RegistroGastoDirecto(idPresupuestoDetalle, idProveedor, idMoneda, fecha.Date, concepto?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
            decimal.Round(monto, 2, MidpointRounding.AwayFromZero), referencia);
    }
}
