using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Entities;

namespace Cobranzas_Vittoria.Interfaces;

public interface IProveedorTerrenoRepository
{
    Task<IEnumerable<ProveedorTerreno>> ListAsync(bool? activo);
    Task<int> UpsertAsync(ProveedorTerrenoUpsertDto dto);
    Task DeleteAsync(int idProveedorTerreno);
}
