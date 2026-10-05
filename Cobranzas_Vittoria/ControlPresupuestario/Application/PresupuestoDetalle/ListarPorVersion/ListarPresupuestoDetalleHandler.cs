using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.ListarPorVersion;

public sealed class ListarPresupuestoDetalleHandler
{
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IPresupuestoDetalleRepository _detalles;

    public ListarPresupuestoDetalleHandler(IPresupuestoVersionRepository versiones, IPresupuestoDetalleRepository detalles)
    {
        _versiones = versiones;
        _detalles = detalles;
    }

    public async Task<IReadOnlyList<PresupuestoDetalleResult>> HandleAsync(ListarPresupuestoDetalleQuery query)
    {
        PresupuestoVersionValidator.ValidarIds(query.IdPresupuesto, query.IdPresupuestoVersion);
        await _versiones.ObtenerDelPresupuestoAsync(query.IdPresupuesto, query.IdPresupuestoVersion);
        return (await _detalles.ListarPorVersionAsync(query.IdPresupuestoVersion)).Select(PresupuestoDetalleResult.Desde).ToList();
    }
}
