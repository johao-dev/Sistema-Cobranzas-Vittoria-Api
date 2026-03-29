using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Entities;

namespace Cobranzas_Vittoria.Interfaces;

public interface ICategoriaGastoRepository
{
    Task<IEnumerable<CategoriaGasto>> ListAsync(bool? activo);
    Task<int> UpsertAsync(CategoriaGastoUpsertDto dto);
    Task DeleteAsync(int idCategoriaGasto);
}
