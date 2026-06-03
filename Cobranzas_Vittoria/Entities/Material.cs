namespace Cobranzas_Vittoria.Entities;
public class Material
{
    public int IdMaterial { get; set; }
    public int IdEspecialidad { get; set; }
    public string? Especialidad { get; set; }
    public string? Codigo { get; set; }
    public string? CodigoProveedor { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal StockMinimo { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
}
