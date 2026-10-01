namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.RegistrarAjuste;

public sealed record RegistrarAjusteCommand(int IdPresupuesto, int IdPresupuestoVersion, int IdPresupuestoDetalle,
    string? Afectacion, string? Direccion, decimal Monto, string? Observacion, DateTime? Fecha);
