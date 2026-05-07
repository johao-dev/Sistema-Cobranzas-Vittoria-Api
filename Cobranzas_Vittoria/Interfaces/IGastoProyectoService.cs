using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Entities;

namespace Cobranzas_Vittoria.Interfaces;

public interface IGastoProyectoService
{
    Task<IEnumerable<GastoProyecto>> ListAsync(string tipoModulo, int? idProyecto, string? concepto, string? estado, bool? activo);
    Task<object?> GetAsync(int idGastoProyecto);
    Task<int> UpsertAsync(string tipoModulo, GastoProyectoUpsertDto dto);
    Task DeleteAsync(int idGastoProyecto);
    Task<IEnumerable<GastoProyectoDocumento>> GetDocumentosAsync(int idGastoProyecto);
    Task SaveDocumentosAsync(int idGastoProyecto, IEnumerable<(string TipoDocumento, string NombreArchivo, string RutaArchivo, string? Extension)> docs);
}
