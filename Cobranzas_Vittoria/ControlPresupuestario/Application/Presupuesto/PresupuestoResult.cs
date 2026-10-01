namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto;

public sealed record PresupuestoResult(int IdPresupuesto, string Codigo, string Nombre, string? Descripcion,
    int IdCentroCosto, string? CodigoCentroCosto, string? NombreCentroCosto, int IdMoneda, string? CodigoMoneda,
    string? NombreMoneda, string? SimboloMoneda, DateTime? FechaInicio, DateTime? FechaFin, bool Activo,
    DateTime? FechaCreacion, DateTime? FechaModificacion, int? IdPresupuestoVersionAprobada,
    int? IdPresupuestoVersionBorrador, int CantidadAprobadas, int CantidadBorradores, string? EstadoElaboracion)
{
    public static PresupuestoResult Desde(Domain.Model.Presupuesto p) => new(p.IdPresupuesto, p.Codigo, p.Nombre,
        p.Descripcion, p.IdCentroCosto, p.CodigoCentroCosto, p.NombreCentroCosto, p.IdMoneda, p.CodigoMoneda,
        p.NombreMoneda, p.SimboloMoneda, p.FechaInicio, p.FechaFin, p.Activo, p.FechaCreacion, p.FechaModificacion,
        p.IdPresupuestoVersionAprobada, p.IdPresupuestoVersionBorrador, p.CantidadAprobadas, p.CantidadBorradores,
        p.EstadoElaboracion);
}
