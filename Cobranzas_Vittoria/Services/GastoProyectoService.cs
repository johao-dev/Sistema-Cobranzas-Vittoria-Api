using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Services;

public class GastoProyectoService : IGastoProyectoService
{
    private readonly IGastoProyectoRepository _repository;
    public GastoProyectoService(IGastoProyectoRepository repository) => _repository = repository;

    public Task<IEnumerable<GastoProyecto>> ListAsync(string tipoModulo, int? idProyecto, string? concepto, string? estado, bool? activo)
        => _repository.ListAsync(NormalizeTipoModulo(tipoModulo), idProyecto, concepto, estado, activo);

    public async Task<object?> GetAsync(int idGastoProyecto)
    {
        var (gasto, documentos) = await _repository.GetAsync(idGastoProyecto);
        return gasto is null ? null : new { gasto, documentos };
    }

    public Task<int> UpsertAsync(string tipoModulo, GastoProyectoUpsertDto dto)
        => _repository.UpsertAsync(NormalizeTipoModulo(tipoModulo), dto);

    public Task DeleteAsync(int idGastoProyecto) => _repository.DeleteAsync(idGastoProyecto);
    public Task<IEnumerable<GastoProyectoDocumento>> GetDocumentosAsync(int idGastoProyecto) => _repository.GetDocumentosAsync(idGastoProyecto);
    public Task SaveDocumentosAsync(int idGastoProyecto, IEnumerable<(string TipoDocumento, string NombreArchivo, string RutaArchivo, string? Extension)> docs)
        => _repository.SaveDocumentosAsync(idGastoProyecto, docs);

    public static string NormalizeTipoModulo(string tipoModulo)
    {
        var value = (tipoModulo ?? string.Empty)
            .Trim()
            .Replace("_", "-")
            .Replace(" ", "-")
            .ToLowerInvariant();

        return value switch
        {
            "terreno" => "Terreno",
            "marketing" => "Marketing",
            "marketing-publicidad" => "Marketing",
            "otros" => "OtrosGastos",
            "otros-gastos" => "OtrosGastos",
            "otrosgastos" => "OtrosGastos",
            "municipales" => "GastosMunicipales",
            "gastos-municipales" => "GastosMunicipales",
            "gastos-municipales-distritales" => "GastosMunicipales",
            "gastosmunicipales" => "GastosMunicipales",
            "gastosmunicipalesdistritales" => "GastosMunicipales",
            _ => throw new InvalidOperationException("Tipo de módulo no válido.")
        };
    }
}
