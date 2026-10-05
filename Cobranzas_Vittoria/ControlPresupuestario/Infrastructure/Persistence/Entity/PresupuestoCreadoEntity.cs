namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Resultado de usp_Presupuesto_Crear.</summary>
public sealed class PresupuestoCreadoEntity
{
    public int IdPresupuesto { get; set; }
    public int IdPresupuestoVersion { get; set; }
    public int NumeroVersion { get; set; }
    public string? EstadoPresupuesto { get; set; }
}
