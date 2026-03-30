using Cobranzas_Vittoria.Dtos.Maestra;

namespace Cobranzas_Vittoria.Interfaces
{
    public interface IUnidadMedidaService
    {
        Task<IEnumerable<UnidadMedidaDto>> ListAsync(bool? activo);
        Task<int> UpsertAsync(UnidadMedidaUpsertDto dto);
    }
}
