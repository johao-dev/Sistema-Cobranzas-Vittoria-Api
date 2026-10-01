namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto.Actualizar;

public sealed record ActualizarPresupuestoCommand(int IdPresupuesto, string Nombre, bool Activo, string? Descripcion,
    DateTime? FechaInicio, DateTime? FechaFin, bool ConfirmarInactivacion = false);
