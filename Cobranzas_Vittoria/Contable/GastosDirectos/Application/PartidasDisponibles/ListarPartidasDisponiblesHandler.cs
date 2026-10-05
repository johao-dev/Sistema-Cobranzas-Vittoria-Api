using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.ValueObject;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.PartidasDisponibles;

public sealed class ListarPartidasDisponiblesHandler
{
    private readonly IGastoDirectoRepository _repository;

    public ListarPartidasDisponiblesHandler(IGastoDirectoRepository repository) => _repository = repository;

    public Task<IReadOnlyList<PartidaDisponibleGasto>> HandleAsync(ListarPartidasDisponiblesQuery query)
    {
        var seccion = SeccionGasto.Normalizar(query.Seccion, requerida: true)!;
        if (query.IdCentroCosto <= 0) throw new ValidacionGastoDirectoException("Selecciona un centro de costo.");
        return _repository.ListarPartidasDisponiblesAsync(seccion, query.IdCentroCosto);
    }
}
