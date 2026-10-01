namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Fila de vw_EjecucionDiariaPorPartida.</summary>
public sealed class EjecucionDiariaEntity
{
    public int IdPresupuesto { get; set; }
    public int IdCatalogoPartida { get; set; }
    public DateTime Fecha { get; set; }
    public decimal MontoEjecutado { get; set; }
}
