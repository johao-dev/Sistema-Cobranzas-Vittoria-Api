using Cobranzas_Vittoria.Dtos.Almacen;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services
{
    public class KardexService : IKardexService
    {
        private readonly IKardexRepository _repo;

        public KardexService(IKardexRepository repo)
        {
            _repo = repo;
        }

        public Task<IEnumerable<dynamic>> ListMovimientosAsync(int? idCompra, int? idMaterial, int? idEspecialidad, string? fechaDesde, string? fechaHasta)
            => _repo.ListMovimientosAsync(idCompra, idMaterial, idEspecialidad, fechaDesde, fechaHasta);

        public Task<object> RegistrarSalidaAsync(KardexSalidaCreateDto dto)
            => _repo.RegistrarSalidaAsync(dto);
    }
}
