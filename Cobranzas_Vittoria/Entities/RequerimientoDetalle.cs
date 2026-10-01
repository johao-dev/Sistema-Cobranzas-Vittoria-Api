namespace Cobranzas_Vittoria.Entities;
public class RequerimientoDetalle
{
    public int IdRequerimientoDetalle { get; set; }
    public int IdRequerimiento { get; set; }
    public int IdMaterial { get; set; }
    public int? IdPresupuestoDetalle { get; set; }
    /// <summary>Partida imputada; null cuando la línea no está imputada al presupuesto.</summary>
    public string? CodigoPartida { get; set; }
    public string? NombrePartida { get; set; }
    public int? IdEspecialidad { get; set; }
    public string? Especialidad { get; set; }
    public string? Material { get; set; }
    public string? UnidadMedida { get; set; }
    public decimal Cantidad { get; set; }
    public string? Observacion { get; set; }
}
