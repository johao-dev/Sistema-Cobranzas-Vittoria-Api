using Cobranzas_Vittoria.Dtos.Requerimientos;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services
{
    public class RequerimientosService : IRequerimientosService
    {
        private readonly IRequerimientosRepository _repo;

        public RequerimientosService(IRequerimientosRepository repo)
        {
            _repo = repo;
        }

        public Task<int> CrearAsync(RequerimientoCreateDto dto)
            => _repo.CrearAsync(dto);

        public async Task<RequerimientoGetResponseDto?> ObtenerAsync(int idRequerimiento)
        {
            var (head, items, validaciones) = await _repo.ObtenerAsync(idRequerimiento);
            if (head == null) return null;

            return new RequerimientoGetResponseDto
            {
                Requerimiento = head,
                Items = items,
                Validaciones = validaciones
            };
        }
    }
}
