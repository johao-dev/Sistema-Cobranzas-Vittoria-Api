namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Agregar;

public sealed record AgregarPresupuestoDetalleCommand(int IdPresupuesto, int IdPresupuestoVersion, int IdCatalogoPartida,
    decimal MontoPresupuestado, string? Observacion);
