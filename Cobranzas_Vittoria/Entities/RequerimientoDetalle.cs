namespace Cobranzas_Vittoria.Entities;
public class RequerimientoDetalle
{
    public int IdRequerimientoDetalle { get; set; }
    public int IdRequerimiento { get; set; }
    public int IdMaterial { get; set; }
    public int? IdPresupuestoDetalle { get; set; }
    public int? IdEspecialidad { get; set; }
    public string? Especialidad { get; set; }
    public string? Material { get; set; }
    public string? UnidadMedida { get; set; }
    public decimal Cantidad { get; set; }
    public string? Observacion { get; set; }
}
