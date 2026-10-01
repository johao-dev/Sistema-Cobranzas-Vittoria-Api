using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Listar;

public sealed class ListarCatalogoPartidaHandler
{
    private readonly ICatalogoPartidaRepository _repository;

    public ListarCatalogoPartidaHandler(ICatalogoPartidaRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<CatalogoPartidaResult>> HandleAsync(ListarCatalogoPartidaQuery q)
    {
        CatalogoPartidaValidator.ValidarFiltro(q.SoloRaices, q.IdPartidaPadre);
        var partidas = await _repository.ListarAsync(new FiltroPartidas(q.Activo, q.IdTipoPartida, q.IdPartidaPadre,
            q.SoloRaices, q.EsHoja, Validacion.Texto(q.Busqueda), q.IdSeccionGasto));
        return partidas.Select(CatalogoPartidaResult.Desde).ToList();
    }
}
