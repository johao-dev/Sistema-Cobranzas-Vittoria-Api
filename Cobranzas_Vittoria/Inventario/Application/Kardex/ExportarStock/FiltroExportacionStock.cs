namespace Cobranzas_Vittoria.Inventario.Application.Kardex.ExportarStock;

/// <summary>Selecciona el stock global o la asignación actual de un proyecto para exportar.</summary>
public sealed record FiltroExportacionStock
{
    public int? IdProyecto { get; init; }
    public int? IdEspecialidad { get; init; }
    public DateOnly? FechaDesde { get; init; }
    public DateOnly? FechaHasta { get; init; }
}
