using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services;

public class GastoAdministrativoService : IGastoAdministrativoService
{
    private readonly IGastoAdministrativoRepository _repository;
    public GastoAdministrativoService(IGastoAdministrativoRepository repository) => _repository = repository;

    public Task<IEnumerable<GastoAdministrativo>> ListAsync(int? idCategoriaGasto, int? idProveedorGastoAdministrativo, bool? activo)
        => _repository.ListAsync(idCategoriaGasto, idProveedorGastoAdministrativo, activo);

    public async Task<object?> GetAsync(int idGastoAdministrativo)
    {
        var (gasto, documentos) = await _repository.GetAsync(idGastoAdministrativo);
        return gasto is null ? null : new { gasto, documentos };
    }

    public Task<int> UpsertAsync(GastoAdministrativoUpsertDto dto) => _repository.UpsertAsync(dto);
    public Task DeleteAsync(int idGastoAdministrativo) => _repository.DeleteAsync(idGastoAdministrativo);
    public Task<IEnumerable<GastoAdministrativoDocumento>> GetDocumentosAsync(int idGastoAdministrativo) => _repository.GetDocumentosAsync(idGastoAdministrativo);
    public Task SaveDocumentosAsync(int idGastoAdministrativo, IEnumerable<(string TipoDocumento, string NombreArchivo, string RutaArchivo, string? Extension)> docs)
        => _repository.SaveDocumentosAsync(idGastoAdministrativo, docs);
}
