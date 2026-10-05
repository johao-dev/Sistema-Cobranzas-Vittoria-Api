using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.Listar;

public sealed class ListarPresupuestoVersionHandler
{
    private readonly IPresupuestoRepository _presupuestos;
    private readonly IPresupuestoVersionRepository _versiones;

    public ListarPresupuestoVersionHandler(IPresupuestoRepository presupuestos, IPresupuestoVersionRepository versiones)
    {
        _presupuestos = presupuestos;
        _versiones = versiones;
    }

    public async Task<IReadOnlyList<PresupuestoVersionResult>> HandleAsync(ListarPresupuestoVersionQuery query)
    {
        PresupuestoValidator.ValidarId(query.IdPresupuesto);
        var versiones = await _versiones.ListarAsync(query.IdPresupuesto);
        if (versiones.Count == 0 && await _presupuestos.ObtenerAsync(query.IdPresupuesto) is null)
            throw new PresupuestoNoEncontradoException(query.IdPresupuesto);
        return versiones.Select(PresupuestoVersionResult.Desde).ToList();
    }
}
