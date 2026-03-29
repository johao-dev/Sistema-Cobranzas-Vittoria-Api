using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Entities;

namespace Cobranzas_Vittoria.Interfaces;

public interface IProveedorGastoAdministrativoService
{
    Task<IEnumerable<ProveedorGastoAdministrativo>> ListAsync(bool? activo, int? idCategoriaGasto);
    Task<int> UpsertAsync(ProveedorGastoAdministrativoUpsertDto dto);
    Task DeleteAsync(int idProveedorGastoAdministrativo);
}
