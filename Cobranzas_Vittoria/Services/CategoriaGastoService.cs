using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services;

public class CategoriaGastoService : ICategoriaGastoService
{
    private readonly ICategoriaGastoRepository _repository;
    public CategoriaGastoService(ICategoriaGastoRepository repository) => _repository = repository;

    public Task<IEnumerable<CategoriaGasto>> ListAsync(bool? activo) => _repository.ListAsync(activo);
    public Task<int> UpsertAsync(CategoriaGastoUpsertDto dto) => _repository.UpsertAsync(dto);
    public Task DeleteAsync(int idCategoriaGasto) => _repository.DeleteAsync(idCategoriaGasto);
}
