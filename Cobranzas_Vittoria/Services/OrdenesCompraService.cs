using Cobranzas_Vittoria.Dtos.OrdenesCompra;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services
{
    public class OrdenesCompraService : IOrdenesCompraService
    {
        private readonly IOrdenesCompraRepository _repo;

        public OrdenesCompraService(IOrdenesCompraRepository repo)
        {
            _repo = repo;
        }

        public async Task<OrdenCompraGenerarResponseDto> GenerarAsync(OrdenCompraGenerarDto dto)
        {
            var res = await _repo.GenerarAsync(dto);

            return new OrdenCompraGenerarResponseDto
            {
                IdOrdenCompra = res.IdOrdenCompra,
                Total = res.Total
            };
        }

        public async Task<OrdenCompraGetResponseDto?> ObtenerAsync(int idOrdenCompra)
        {
            var (head, items) = await _repo.ObtenerAsync(idOrdenCompra);
            if (head == null) return null;

            return new OrdenCompraGetResponseDto
            {
                OrdenCompra = head,
                Items = items
            };
        }
    }
}
