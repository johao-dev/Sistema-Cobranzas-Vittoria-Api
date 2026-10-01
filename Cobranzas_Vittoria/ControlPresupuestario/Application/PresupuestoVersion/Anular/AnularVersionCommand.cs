namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.Anular;

public sealed record AnularVersionCommand(int IdPresupuesto, int IdPresupuestoVersion, string? Motivo);
