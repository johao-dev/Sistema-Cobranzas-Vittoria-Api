namespace Cobranzas_Vittoria.Dtos.Compras;
public class RequerimientoCreateDto
{
    public string NumeroRequerimiento { get; set; } = string.Empty;
    public DateTime FechaRequerimiento { get; set; }
    public int IdProyecto { get; set; }
    public string? Descripcion { get; set; }
    public DateTime? FechaEntrega { get; set; }
    public int IdUsuarioSolicitante { get; set; }
    public string? Observacion { get; set; }
    public List<RequerimientoDetalleCreateDto> Items { get; set; } = new();
}
public class RequerimientoDetalleCreateDto
{
    public int IdMaterial { get; set; }
    public int? IdPresupuestoDetalle { get; set; }
    public decimal Cantidad { get; set; }
    public string? Observacion { get; set; }
}
