using Cobranzas_Vittoria.Dtos.Valorizaciones;

namespace Cobranzas_Vittoria.Interfaces
{
    public interface IValorizacionService
    {
        Task<object> ListConfiguracionesAsync(int? idProyecto, int? idProveedor, int? idEspecialidad);
        Task<object> UpsertConfiguracionAsync(ProveedorEspecialidadCotizacionUpsertDto dto);
        Task<object> UpsertReglaProveedorAsync(ProveedorReglaValorizacionUpsertDto dto);
        Task<object> ListAsync(int? idProyecto, int? idProveedor, int? idEspecialidad);
        Task<object> GetByIdAsync(int idValorizacion);
        Task<object> UpsertAsync(ValorizacionUpsertDto dto);
        Task<object> UpsertDetalleAsync(ValorizacionDetalleUpsertDto dto);
        Task<object> DeleteDetalleAsync(int idDetalle, string usuario);
    }
}
