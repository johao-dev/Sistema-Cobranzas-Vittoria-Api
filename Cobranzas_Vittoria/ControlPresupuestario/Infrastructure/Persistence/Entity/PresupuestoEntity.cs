namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Fila de usp_Presupuesto_Listar / usp_Presupuesto_Obtener.</summary>
public sealed class PresupuestoEntity
{
    public int IdPresupuesto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdCentroCosto { get; set; }
    public string? CodigoCentroCosto { get; set; }
    public string? NombreCentroCosto { get; set; }
    public int IdMoneda { get; set; }
    public string? CodigoMoneda { get; set; }
    public string? NombreMoneda { get; set; }
    public string? SimboloMoneda { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public bool Activo { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public int? IdPresupuestoVersionAprobada { get; set; }
    public int? IdPresupuestoVersionBorrador { get; set; }
    public int CantidadAprobadas { get; set; }
    public int CantidadBorradores { get; set; }
    public string? EstadoElaboracion { get; set; }
}
