namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Resultado de usp_PresupuestoDetalle_CargaLote.</summary>
public sealed class CargaLoteEntity
{
    public int IdPresupuestoVersion { get; set; }
    public int Agregados { get; set; }
    public int Actualizados { get; set; }
    public int Eliminados { get; set; }
    public int PartidasEnVersion { get; set; }
    public decimal MontoTotal { get; set; }
}
