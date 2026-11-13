namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>Resultado confirmado por SQL de la aprobación, con la fecha persistida en UTC.</summary>
public sealed record VersionAprobada(
    int IdPresupuesto,
    int IdPresupuestoVersion,
    int NumeroVersion,
    DateTime FechaAprobacionUtc);
