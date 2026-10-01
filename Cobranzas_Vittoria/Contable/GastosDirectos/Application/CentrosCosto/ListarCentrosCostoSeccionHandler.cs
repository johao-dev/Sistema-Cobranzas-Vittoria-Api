using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.ValueObject;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.CentrosCosto;

public sealed class ListarCentrosCostoSeccionHandler
{
    private readonly IGastoDirectoRepository _repository;

    public ListarCentrosCostoSeccionHandler(IGastoDirectoRepository repository) => _repository = repository;

    public Task<IReadOnlyList<CentroCostoSeccion>> HandleAsync(ListarCentrosCostoSeccionQuery query)
    {
        var seccion = SeccionGasto.Normalizar(query.Seccion, requerida: true)!;
        return _repository.ListarCentrosCostoAsync(seccion);
    }
}
