using Cobranzas_Vittoria.Dtos.Seguridad;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services
{
    public class RolService : IRolService
    {
        private readonly IRolRepository _repo;
        public RolService(IRolRepository repo) => _repo = repo;
        public Task<IEnumerable<Rol>> ListAsync(bool? activo = null) => _repo.ListAsync(activo);
        public Task<int> UpsertAsync(RolUpsertDto dto) => _repo.UpsertAsync(dto);
    }
}
