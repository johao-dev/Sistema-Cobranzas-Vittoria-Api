using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal.Obtener;

public sealed class ObtenerMovimientoHandler
{
    private readonly IMovimientoPresupuestalRepository _repository;

    public ObtenerMovimientoHandler(IMovimientoPresupuestalRepository repository) => _repository = repository;

    public async Task<MovimientoPresupuestalResult> HandleAsync(ObtenerMovimientoQuery query)
    {
        Validacion.Id(query.IdMovimientoPresupuestal, "IdMovimientoPresupuestal");
        var movimiento = await _repository.ObtenerAsync(query.IdMovimientoPresupuestal)
            ?? throw new MovimientoPresupuestalNoEncontradoException(query.IdMovimientoPresupuestal);
        return MovimientoPresupuestalResult.Desde(movimiento);
    }
}
