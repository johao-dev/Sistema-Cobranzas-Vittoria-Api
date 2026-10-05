using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Documentos.Listar;

public sealed class ListarDocumentosHandler
{
    private readonly IGastoDirectoRepository _repository;

    public ListarDocumentosHandler(IGastoDirectoRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<GastoDirectoDocumento>> HandleAsync(ListarDocumentosQuery query)
    {
        GastoDirectoValidator.ValidarId(query.IdGastoDirecto);
        return (await _repository.ObtenerAsync(query.IdGastoDirecto)).Documentos;
    }
}
