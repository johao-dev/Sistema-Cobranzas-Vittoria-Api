using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Entities;

namespace Cobranzas_Vittoria.Interfaces;

public interface IGastoAdministrativoService
{
    Task<IEnumerable<GastoAdministrativo>> ListAsync(int? idCategoriaGasto, int? idProveedorGastoAdministrativo, bool? activo);
    Task<object?> GetAsync(int idGastoAdministrativo);
    Task<int> UpsertAsync(GastoAdministrativoUpsertDto dto);
    Task DeleteAsync(int idGastoAdministrativo);
    Task<IEnumerable<GastoAdministrativoDocumento>> GetDocumentosAsync(int idGastoAdministrativo);
    Task SaveDocumentosAsync(int idGastoAdministrativo, IEnumerable<(string TipoDocumento, string NombreArchivo, string RutaArchivo, string? Extension)> docs);
}
