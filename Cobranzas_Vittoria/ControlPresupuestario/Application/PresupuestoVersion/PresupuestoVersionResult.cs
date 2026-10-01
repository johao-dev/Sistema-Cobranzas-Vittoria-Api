namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion;

public sealed record PresupuestoVersionResult(int IdPresupuestoVersion, int IdPresupuesto, int NumeroVersion,
    int IdEstadoPresupuesto, string Estado, string? NombreEstado, string? Descripcion, string? MotivoCambio,
    string? MotivoAnulacion, DateTime? FechaCreacion, DateTime? FechaAprobacion, DateTime? FechaAnulacion,
    string? UsuarioCreacion, string? UsuarioAprobacion, string? UsuarioAnulacion, string? CodigoPresupuesto,
    string? NombrePresupuesto, int IdCentroCosto, int IdMoneda, string? CodigoMoneda, string? SimboloMoneda)
{
    public static PresupuestoVersionResult Desde(Domain.Model.PresupuestoVersion v) => new(v.IdPresupuestoVersion,
        v.IdPresupuesto, v.NumeroVersion, v.IdEstadoPresupuesto, v.Estado, v.NombreEstado, v.Descripcion, v.MotivoCambio,
        v.MotivoAnulacion, v.FechaCreacion, v.FechaAprobacion, v.FechaAnulacion, v.UsuarioCreacion, v.UsuarioAprobacion,
        v.UsuarioAnulacion, v.CodigoPresupuesto, v.NombrePresupuesto, v.IdCentroCosto, v.IdMoneda, v.CodigoMoneda,
        v.SimboloMoneda);
}
