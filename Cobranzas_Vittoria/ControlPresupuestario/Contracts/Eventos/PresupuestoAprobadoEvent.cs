namespace Cobranzas_Vittoria.ControlPresupuestario.Contracts.Eventos;

/// <summary>Ocurrencia posterior al commit de la aprobación de una versión de presupuesto.</summary>
public sealed record PresupuestoAprobadoEvent(
    Guid IdEvento,
    int IdPresupuesto,
    int IdPresupuestoVersion,
    int NumeroVersion,
    DateTime FechaAprobacionUtc,
    DateTime FechaOcurrenciaUtc);
