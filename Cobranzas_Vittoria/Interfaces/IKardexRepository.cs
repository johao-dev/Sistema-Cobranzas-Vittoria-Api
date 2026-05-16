using Cobranzas_Vittoria.Dtos.Almacen;

namespace Cobranzas_Vittoria.Interfaces
{
    public interface IKardexRepository
    {
        Task<IEnumerable<dynamic>> ListMovimientosAsync(int? idCompra, int? idMaterial, int? idEspecialidad, string? fechaDesde, string? fechaHasta);
        Task<object> RegistrarSalidaAsync(KardexSalidaCreateDto dto);
    }
}
