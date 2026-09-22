using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services;

public sealed class GastoDirectoService : IGastoDirectoService
{
    private static readonly HashSet<string> Estados =
        new(StringComparer.Ordinal) { "REGISTRADO", "CONFIRMADO", "ANULADO" };
    private readonly IGastoDirectoRepository _repository;
    public GastoDirectoService(IGastoDirectoRepository repository) => _repository = repository;

    public Task<IEnumerable<GastoDirecto>> ListarAsync(string? estado, int? idProveedor,
        int? idCentroCosto, DateTime? desde, DateTime? hasta)
    {
        var normalizado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (normalizado is not null && !Estados.Contains(normalizado))
            throw new ArgumentException("Estado debe ser REGISTRADO, CONFIRMADO o ANULADO.", nameof(estado));
        if (desde.HasValue && hasta.HasValue && hasta.Value.Date < desde.Value.Date)
            throw new ArgumentException("Hasta no puede ser anterior a Desde.", nameof(hasta));
        return _repository.ListarAsync(normalizado, idProveedor, idCentroCosto, desde, hasta);
    }

    public async Task<object?> ObtenerAsync(int id)
    {
        var (gasto, documentos) = await _repository.ObtenerAsync(id);
        return gasto is null ? null : new { gasto, documentos };
    }

    public Task<int> CrearAsync(GastoDirectoUpsertDto dto) => _repository.CrearAsync(dto);
    public Task<int> ActualizarAsync(int id, GastoDirectoUpsertDto dto) => _repository.ActualizarAsync(id, dto);
    public Task ConfirmarAsync(int id) => _repository.ConfirmarAsync(id);
    public Task AnularAsync(int id) => _repository.AnularAsync(id);
    public Task<IReadOnlyList<GastoDirectoDocumento>> ListarDocumentosAsync(int id)
        => _repository.ListarDocumentosAsync(id);

    public Task RegistrarDocumentosAsync(int id, IReadOnlyCollection<GastoDirectoDocumentoNuevo> documentos)
    {
        if (documentos.Count == 0) throw new ArgumentException("Debe adjuntar al menos un documento.");
        if (documentos.Any(x => x.TipoDocumento is not ("Factura" or "Pago")))
            throw new ArgumentException("TipoDocumento debe ser Factura o Pago.");
        return _repository.RegistrarDocumentosAsync(id, documentos);
    }
}
