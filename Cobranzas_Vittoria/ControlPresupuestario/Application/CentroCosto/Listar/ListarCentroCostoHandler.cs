using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Listar;

public sealed class ListarCentroCostoHandler
{
    private readonly ICentroCostoRepository _repository;

    public ListarCentroCostoHandler(ICentroCostoRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<CentroCostoResult>> HandleAsync(ListarCentroCostoQuery query)
    {
        CentroCostoValidator.ValidarFiltro(query.IdTipoCentroCosto, query.IdProyecto);
        var centros = await _repository.ListarAsync(query.Activo, query.IdTipoCentroCosto, query.IdProyecto,
            Validacion.Texto(query.Busqueda));
        return centros.Select(CentroCostoResult.Desde).ToList();
    }
}
