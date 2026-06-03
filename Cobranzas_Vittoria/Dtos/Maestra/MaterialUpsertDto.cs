namespace Cobranzas_Vittoria.Dtos.Maestra;
public class MaterialUpsertDto
{
    public int? IdMaterial { get; set; }
    public int IdEspecialidad { get; set; }
    public string? Codigo { get; set; }
    public string? CodigoProveedor { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal StockMinimo { get; set; }
    public bool Activo { get; set; } = true;
}
