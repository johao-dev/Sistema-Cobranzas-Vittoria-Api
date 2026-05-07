namespace Cobranzas_Vittoria.Dtos.Contable;

public class CotizacionMaterialesUpsertDto
{
    public int IdProyecto { get; set; }
    public List<CotizacionMaterialEspecialidadItemDto> Items { get; set; } = new();
}

public class CotizacionMaterialEspecialidadItemDto
{
    public int IdEspecialidad { get; set; }
    public decimal Cotizacion { get; set; }
}
