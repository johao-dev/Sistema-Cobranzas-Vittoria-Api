using Cobranzas_Vittoria.Entities;

namespace Cobranzas_Vittoria.Interfaces
{
    public interface IOrdenesCompraRepository
    {
        Task<OrdenCompraGenerada> GenerarAsync(Cobranzas_Vittoria.Dtos.OrdenesCompra.OrdenCompraGenerarDto dto);

        Task<(OrdenCompra? ordenCompra,
              List<OrdenCompraDetalle> items)>
              ObtenerAsync(int idOrdenCompra);
    }
}
