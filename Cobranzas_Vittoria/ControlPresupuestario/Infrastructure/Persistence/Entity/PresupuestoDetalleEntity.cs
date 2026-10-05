namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Fila de usp_PresupuestoDetalle_ListarPorVersion.</summary>
public sealed class PresupuestoDetalleEntity
{
    public int IdPresupuestoDetalle { get; set; }
    public int IdPresupuestoVersion { get; set; }
    public int IdCatalogoPartida { get; set; }
    public string? CodigoPartida { get; set; }
    public string? NombrePartida { get; set; }
    public int? IdPartidaPadre { get; set; }
    public int Nivel { get; set; }
    public int IdTipoPartida { get; set; }
    public string? CodigoTipoPartida { get; set; }
    public string? NombreTipoPartida { get; set; }
    public bool PartidaActiva { get; set; }
    public bool EsHoja { get; set; }
    public decimal MontoPresupuestado { get; set; }
    public string? Observacion { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
}
