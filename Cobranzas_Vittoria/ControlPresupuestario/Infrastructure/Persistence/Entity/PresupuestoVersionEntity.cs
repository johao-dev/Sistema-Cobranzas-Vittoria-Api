namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Fila de usp_PresupuestoVersion_Listar / usp_PresupuestoVersion_Obtener.</summary>
public sealed class PresupuestoVersionEntity
{
    public int IdPresupuestoVersion { get; set; }
    public int IdPresupuesto { get; set; }
    public int NumeroVersion { get; set; }
    public int IdEstadoPresupuesto { get; set; }
    public string? EstadoPresupuesto { get; set; }
    public string? NombreEstadoPresupuesto { get; set; }
    public string? Descripcion { get; set; }
    public string? MotivoCambio { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? UsuarioCreacion { get; set; }
    public string? UsuarioAprobacion { get; set; }
    public string? UsuarioAnulacion { get; set; }
    public string? CodigoPresupuesto { get; set; }
    public string? NombrePresupuesto { get; set; }
    public int IdCentroCosto { get; set; }
    public int IdMoneda { get; set; }
    public string? CodigoMoneda { get; set; }
    public string? SimboloMoneda { get; set; }
}
