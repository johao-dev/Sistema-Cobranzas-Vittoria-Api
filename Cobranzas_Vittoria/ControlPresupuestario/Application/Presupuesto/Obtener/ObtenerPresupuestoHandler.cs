using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto.Obtener;

public sealed class ObtenerPresupuestoHandler
{
    private readonly IPresupuestoRepository _repository;

    public ObtenerPresupuestoHandler(IPresupuestoRepository repository) => _repository = repository;

    public async Task<PresupuestoResult> HandleAsync(ObtenerPresupuestoQuery query)
    {
        PresupuestoValidator.ValidarId(query.IdPresupuesto);
        var presupuesto = await _repository.ObtenerAsync(query.IdPresupuesto)
            ?? throw new PresupuestoNoEncontradoException(query.IdPresupuesto);
        return PresupuestoResult.Desde(presupuesto);
    }
}
