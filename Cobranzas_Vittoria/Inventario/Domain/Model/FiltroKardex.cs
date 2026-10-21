namespace Cobranzas_Vittoria.Inventario.Domain.Model;

/// <summary>Filtros opcionales para consultar entradas y salidas de Kardex.</summary>
public sealed record FiltroKardex
{
    public int? IdEspecialidad { get; init; }

    public int? IdProyecto { get; init; }

    public int? IdProveedor { get; init; }

    public DateOnly? FechaDesde { get; init; }

    public DateOnly? FechaHasta { get; init; }
}
