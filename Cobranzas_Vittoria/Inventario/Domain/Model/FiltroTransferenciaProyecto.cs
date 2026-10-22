namespace Cobranzas_Vittoria.Inventario.Domain.Model;

public sealed class FiltroTransferenciaProyecto
{
    public int? IdTransferencia { get; set; }
    public int? IdProyectoOrigen { get; set; }
    public int? IdProyectoDestino { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
}
