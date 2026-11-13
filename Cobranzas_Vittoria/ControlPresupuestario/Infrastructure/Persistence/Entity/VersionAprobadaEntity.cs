namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Fila devuelta por usp_PresupuestoVersion_Aprobar después de confirmar.</summary>
public sealed class VersionAprobadaEntity
{
    public int IdPresupuesto { get; set; }
    public int IdPresupuestoVersionAprobada { get; set; }
    public int NumeroVersion { get; set; }
    public DateTime FechaAprobacion { get; set; }
}
