namespace Cobranzas_Vittoria.Inventario.Domain.Model;

public sealed class FiltroStockProyecto
{
    public int? IdProyecto { get; set; }
    public int? IdEspecialidad { get; set; }
    public int? IdMaterial { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
}
