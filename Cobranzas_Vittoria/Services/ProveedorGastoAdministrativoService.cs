using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services;

public class ProveedorGastoAdministrativoService : IProveedorGastoAdministrativoService
{
    private readonly IProveedorGastoAdministrativoRepository _repository;
    public ProveedorGastoAdministrativoService(IProveedorGastoAdministrativoRepository repository) => _repository = repository;

    public Task<IEnumerable<ProveedorGastoAdministrativo>> ListAsync(bool? activo, int? idCategoriaGasto) => _repository.ListAsync(activo, idCategoriaGasto);
    public Task<int> UpsertAsync(ProveedorGastoAdministrativoUpsertDto dto) => _repository.UpsertAsync(dto);
    public Task DeleteAsync(int idProveedorGastoAdministrativo) => _repository.DeleteAsync(idProveedorGastoAdministrativo);
}
