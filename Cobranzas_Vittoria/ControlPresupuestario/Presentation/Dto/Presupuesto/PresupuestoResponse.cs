using Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.Presupuesto;

/// <summary>Presupuesto. CentroCosto es el nombre del centro de costo y Moneda su código (PEN, USD).</summary>
public sealed record PresupuestoResponse(int IdPresupuesto, string Codigo, string Nombre, string? Descripcion,
    int IdCentroCosto, string? CodigoCentroCosto, string? CentroCosto, int IdMoneda, string? Moneda, string? NombreMoneda,
    string? SimboloMoneda, DateTime? FechaInicio, DateTime? FechaFin, bool Activo, DateTime? FechaCreacion,
    DateTime? FechaModificacion, int? IdPresupuestoVersionAprobada, int? IdPresupuestoVersionBorrador,
    int CantidadAprobadas, int CantidadBorradores, string? EstadoElaboracion)
{
    public static PresupuestoResponse Desde(PresupuestoResult r) => new(r.IdPresupuesto, r.Codigo, r.Nombre, r.Descripcion,
        r.IdCentroCosto, r.CodigoCentroCosto, r.NombreCentroCosto, r.IdMoneda, r.CodigoMoneda, r.NombreMoneda,
        r.SimboloMoneda, r.FechaInicio, r.FechaFin, r.Activo, r.FechaCreacion, r.FechaModificacion,
        r.IdPresupuestoVersionAprobada, r.IdPresupuestoVersionBorrador, r.CantidadAprobadas, r.CantidadBorradores,
        r.EstadoElaboracion);
}
