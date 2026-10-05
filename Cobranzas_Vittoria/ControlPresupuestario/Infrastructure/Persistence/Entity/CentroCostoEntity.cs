namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Fila de usp_CentroCosto_Listar / usp_CentroCosto_Obtener.</summary>
public sealed class CentroCostoEntity
{
    public int IdCentroCosto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? IdProyecto { get; set; }
    public string? NombreProyecto { get; set; }
    public int IdTipoCentroCosto { get; set; }
    public string? CodigoTipoCentroCosto { get; set; }
    public string? NombreTipoCentroCosto { get; set; }
    public bool Activo { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
}
