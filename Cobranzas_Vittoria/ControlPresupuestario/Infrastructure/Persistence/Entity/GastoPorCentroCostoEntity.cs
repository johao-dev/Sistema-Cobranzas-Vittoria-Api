namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Fila de vw_GastosPorCentroCosto.</summary>
public sealed class GastoPorCentroCostoEntity
{
    public int IdCentroCosto { get; set; }
    public string? CodigoCentroCosto { get; set; }
    public string? NombreCentroCosto { get; set; }
    public int IdPresupuesto { get; set; }
    public string? CodigoPresupuesto { get; set; }
    public string? NombrePresupuesto { get; set; }
    public int IdMoneda { get; set; }
    public string? CodigoMoneda { get; set; }
    public string? SimboloMoneda { get; set; }
    public int IdPresupuestoVersion { get; set; }
    public int NumeroVersion { get; set; }
    public string? EstadoPresupuesto { get; set; }
    public decimal MontoPresupuestado { get; set; }
    public decimal MontoComprometido { get; set; }
    public decimal MontoEjecutado { get; set; }
    public decimal SaldoDisponible { get; set; }
    public decimal PorcentajeEjecutado { get; set; }
    public bool Excedido { get; set; }
}
