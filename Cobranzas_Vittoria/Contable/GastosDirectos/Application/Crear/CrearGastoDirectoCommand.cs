namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Crear;

public sealed record CrearGastoDirectoCommand(int IdPresupuestoDetalle, int? IdProveedor, int IdMoneda, DateTime Fecha, string? Concepto,
    string? Descripcion, decimal Monto, int? IdMonedaOriginal, decimal? MontoOriginal, decimal? TipoCambio,
    DateTime? FechaTipoCambio);
