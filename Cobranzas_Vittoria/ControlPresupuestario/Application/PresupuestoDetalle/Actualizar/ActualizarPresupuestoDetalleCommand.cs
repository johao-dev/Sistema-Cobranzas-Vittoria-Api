namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Actualizar;

public sealed record ActualizarPresupuestoDetalleCommand(int IdPresupuesto, int IdPresupuestoVersion,
    int IdPresupuestoDetalle, decimal MontoPresupuestado, string? Observacion);
