namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Resultado de usp_PresupuestoVersion_CrearNueva.</summary>
public sealed class VersionCreadaEntity
{
    public int IdPresupuesto { get; set; }
    public int IdPresupuestoVersion { get; set; }
    public int NumeroVersion { get; set; }
    public string? EstadoPresupuesto { get; set; }
    public int? IdPresupuestoVersionBase { get; set; }
    public int CantidadDetallesCopiados { get; set; }
}
