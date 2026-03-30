using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services
{
    public class UnidadMedidaService : IUnidadMedidaService
    {
        private readonly IUnidadMedidaRepository _repo;

        public UnidadMedidaService(IUnidadMedidaRepository repo)
        {
            _repo = repo;
        }

        public Task<IEnumerable<UnidadMedidaDto>> ListAsync(bool? activo)
            => _repo.ListAsync(activo);

        public Task<int> UpsertAsync(UnidadMedidaUpsertDto dto)
            => _repo.UpsertAsync(dto);
    }
}
