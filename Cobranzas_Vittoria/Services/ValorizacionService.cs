using Cobranzas_Vittoria.Dtos.Valorizaciones;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services
{
    public class ValorizacionService : IValorizacionService
    {
        private readonly IValorizacionRepository _repo;

        public ValorizacionService(IValorizacionRepository repo)
        {
            _repo = repo;
        }

        public Task<object> ListConfiguracionesAsync(int? idProyecto, int? idProveedor, int? idEspecialidad) => _repo.ListConfiguracionesAsync(idProyecto, idProveedor, idEspecialidad);
        public Task<object> UpsertConfiguracionAsync(ProveedorEspecialidadCotizacionUpsertDto dto) => _repo.UpsertConfiguracionAsync(dto);
        public Task<object> UpsertReglaProveedorAsync(ProveedorReglaValorizacionUpsertDto dto) => _repo.UpsertReglaProveedorAsync(dto);
        public Task<object> ListAsync(int? idProyecto, int? idProveedor, int? idEspecialidad) => _repo.ListAsync(idProyecto, idProveedor, idEspecialidad);
        public Task<object> GetByIdAsync(int idValorizacion) => _repo.GetByIdAsync(idValorizacion);
        public Task<object> UpsertAsync(ValorizacionUpsertDto dto) => _repo.UpsertAsync(dto);
        public Task<object> UpsertDetalleAsync(ValorizacionDetalleUpsertDto dto) => _repo.UpsertDetalleAsync(dto);
        public Task<object> DeleteDetalleAsync(int idDetalle, string usuario) => _repo.DeleteDetalleAsync(idDetalle, usuario);
    }
}
