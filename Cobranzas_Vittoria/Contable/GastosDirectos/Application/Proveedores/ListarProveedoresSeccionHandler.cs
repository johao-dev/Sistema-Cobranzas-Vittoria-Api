using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.ValueObject;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Proveedores;

public sealed class ListarProveedoresSeccionHandler
{
    private readonly IGastoDirectoRepository _repository;

    public ListarProveedoresSeccionHandler(IGastoDirectoRepository repository) => _repository = repository;

    public Task<IReadOnlyList<ProveedorSeccion>> HandleAsync(ListarProveedoresSeccionQuery query)
    {
        var seccion = SeccionGasto.Normalizar(query.Seccion, requerida: true)!;
        return _repository.ListarProveedoresAsync(seccion);
    }
}
