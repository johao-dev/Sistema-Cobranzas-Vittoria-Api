using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.Obtener;

public sealed class ObtenerPresupuestoVersionHandler
{
    private readonly IPresupuestoVersionRepository _repository;

    public ObtenerPresupuestoVersionHandler(IPresupuestoVersionRepository repository) => _repository = repository;

    public async Task<PresupuestoVersionResult> HandleAsync(ObtenerPresupuestoVersionQuery query)
    {
        PresupuestoVersionValidator.ValidarIds(query.IdPresupuesto, query.IdPresupuestoVersion);
        var version = await _repository.ObtenerDelPresupuestoAsync(query.IdPresupuesto, query.IdPresupuestoVersion);
        return PresupuestoVersionResult.Desde(version);
    }
}
