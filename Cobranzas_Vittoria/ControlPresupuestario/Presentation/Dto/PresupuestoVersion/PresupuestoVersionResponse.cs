using Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.PresupuestoVersion;

public sealed record PresupuestoVersionResponse(int IdPresupuestoVersion, int IdPresupuesto, int NumeroVersion, string Estado,
    string? NombreEstado, string? Descripcion, string? MotivoCambio, string? MotivoAnulacion, DateTime? FechaCreacion,
    DateTime? FechaAprobacion, DateTime? FechaAnulacion, string? UsuarioCreacion, string? UsuarioAprobacion,
    string? UsuarioAnulacion, string? CodigoPresupuesto, string? NombrePresupuesto, int IdCentroCosto, int IdMoneda,
    string? Moneda, string? SimboloMoneda)
{
    public static PresupuestoVersionResponse Desde(PresupuestoVersionResult r) => new(r.IdPresupuestoVersion, r.IdPresupuesto,
        r.NumeroVersion, r.Estado, r.NombreEstado, r.Descripcion, r.MotivoCambio, r.MotivoAnulacion, r.FechaCreacion,
        r.FechaAprobacion, r.FechaAnulacion, r.UsuarioCreacion, r.UsuarioAprobacion, r.UsuarioAnulacion,
        r.CodigoPresupuesto, r.NombrePresupuesto, r.IdCentroCosto, r.IdMoneda, r.CodigoMoneda, r.SimboloMoneda);
}
