using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services
{
    public class MaterialService : IMaterialService
    {
        private readonly IMaterialRepository _repo;
        public MaterialService(IMaterialRepository repo) => _repo = repo;
        public Task<IEnumerable<Material>> ListAsync(bool? activo, int? idEspecialidad) => _repo.ListAsync(activo, idEspecialidad);
        public Task<Material?> GetAsync(int idMaterial) => _repo.GetAsync(idMaterial);
        public Task<string> GetSiguienteCodigoAsync() => _repo.GetSiguienteCodigoAsync();
        public Task<int> UpsertAsync(MaterialUpsertDto dto) => _repo.UpsertAsync(dto);
    }
}
