using Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto.Crear;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.Presupuesto;

public sealed record CrearPresupuestoResponse(int IdPresupuesto, int IdPresupuestoVersion, int NumeroVersion, string Estado)
{
    public static CrearPresupuestoResponse Desde(CrearPresupuestoResult r)
        => new(r.IdPresupuesto, r.IdPresupuestoVersion, r.NumeroVersion, r.Estado);
}
