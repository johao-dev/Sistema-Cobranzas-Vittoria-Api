namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.CrearNueva;

/// <summary>No se envían partidas: el SP copia el snapshot completo de la versión APROBADA vigente.</summary>
public sealed record CrearNuevaVersionCommand(int IdPresupuesto, string? Descripcion, string? MotivoCambio);
