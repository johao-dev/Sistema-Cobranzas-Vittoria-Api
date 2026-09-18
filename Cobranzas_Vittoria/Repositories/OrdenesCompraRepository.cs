using Cobranzas_Vittoria.Dtos.OrdenesCompra;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Repositories;

/// <summary>Adaptador del contrato anterior al flujo normalizado de Compras.</summary>
public class OrdenesCompraRepository(IOrdenCompraRepository repository) : IOrdenesCompraRepository
{
    public async Task<OrdenCompraGenerada> GenerarAsync(OrdenCompraGenerarDto dto)
    {
        var resultado = await repository.CrearAsync(dto);
        return new OrdenCompraGenerada { IdOrdenCompra = resultado.IdOrdenCompra, Total = resultado.Total };
    }

    public async Task<(OrdenCompra? ordenCompra, List<OrdenCompraDetalle> items)> ObtenerAsync(int idOrdenCompra)
    {
        var (head, items, _) = await repository.GetAsync(idOrdenCompra);
        return (head, items);
    }
}
