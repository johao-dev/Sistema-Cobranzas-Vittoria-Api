using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services;

public class ProveedorTerrenoService : IProveedorTerrenoService
{
    private readonly IProveedorTerrenoRepository _repository;
    public ProveedorTerrenoService(IProveedorTerrenoRepository repository) => _repository = repository;

    public Task<IEnumerable<ProveedorTerreno>> ListAsync(bool? activo) => _repository.ListAsync(activo);
    public Task<int> UpsertAsync(ProveedorTerrenoUpsertDto dto) => _repository.UpsertAsync(dto);
    public Task DeleteAsync(int idProveedorTerreno) => _repository.DeleteAsync(idProveedorTerreno);
}
