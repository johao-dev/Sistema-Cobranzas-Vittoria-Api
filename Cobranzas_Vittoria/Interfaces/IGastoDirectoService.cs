using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Entities;

namespace Cobranzas_Vittoria.Interfaces;

public interface IGastoDirectoService
{
    Task<IEnumerable<GastoDirecto>> ListarAsync(string? estado, int? idProveedor,
        int? idCentroCosto, DateTime? desde, DateTime? hasta);
    Task<object?> ObtenerAsync(int id);
    Task<int> CrearAsync(GastoDirectoUpsertDto dto);
    Task<int> ActualizarAsync(int id, GastoDirectoUpsertDto dto);
    Task ConfirmarAsync(int id);
    Task AnularAsync(int id);
    Task<IReadOnlyList<GastoDirectoDocumento>> ListarDocumentosAsync(int id);
    Task RegistrarDocumentosAsync(int id, IReadOnlyCollection<GastoDirectoDocumentoNuevo> documentos);
}
