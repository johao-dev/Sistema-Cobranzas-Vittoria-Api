using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.ValueObject;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Listar;

public sealed class ListarGastosDirectosHandler
{
    private readonly IGastoDirectoRepository _repository;

    public ListarGastosDirectosHandler(IGastoDirectoRepository repository) => _repository = repository;

    public Task<IReadOnlyList<GastoDirecto>> HandleAsync(ListarGastosDirectosQuery q)
    {
        var seccion = SeccionGasto.Normalizar(q.Seccion, requerida: false);
        var estado = EstadoGastoDirecto.Normalizar(q.Estado);
        if (q.Desde.HasValue && q.Hasta.HasValue && q.Hasta.Value.Date < q.Desde.Value.Date)
            throw new ValidacionGastoDirectoException("Hasta no puede ser anterior a Desde.");
        return _repository.ListarAsync(new FiltroGastosDirectos(estado, q.IdProveedor, q.IdCentroCosto, q.Desde?.Date,
            q.Hasta?.Date, seccion));
    }
}
