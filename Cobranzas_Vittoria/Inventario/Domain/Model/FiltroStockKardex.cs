namespace Cobranzas_Vittoria.Inventario.Domain.Model;

/// <summary>Filtros del stock global por material, con la especialidad derivada de Material; el proyecto no segmenta stock.</summary>
public sealed record FiltroStockKardex
{
    public int? IdEspecialidad { get; init; }

    public DateOnly? FechaDesde { get; init; }

    public DateOnly? FechaHasta { get; init; }
}
