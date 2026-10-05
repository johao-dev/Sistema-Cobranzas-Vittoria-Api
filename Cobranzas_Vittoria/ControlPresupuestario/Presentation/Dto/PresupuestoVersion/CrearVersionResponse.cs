using Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.CrearNueva;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.PresupuestoVersion;

public sealed record CrearVersionResponse(int IdPresupuestoVersion, int NumeroVersion, string Estado, int IdPresupuesto,
    int? IdPresupuestoVersionBase, int CantidadDetallesCopiados)
{
    public static CrearVersionResponse Desde(CrearNuevaVersionResult r) => new(r.IdPresupuestoVersion, r.NumeroVersion,
        r.Estado, r.IdPresupuesto, r.IdPresupuestoVersionBase, r.CantidadDetallesCopiados);
}
