namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Actualizar;

public sealed record ActualizarGastoDirectoCommand(int IdGastoDirecto, int IdPresupuestoDetalle, int? IdProveedor, int IdMoneda, DateTime Fecha, string? Concepto,
    string? Descripcion, decimal Monto, string? Seccion, int? IdMonedaOriginal, decimal? MontoOriginal, decimal? TipoCambio,
    DateTime? FechaTipoCambio);
