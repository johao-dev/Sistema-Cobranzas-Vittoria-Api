using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;

/// <summary>Puerto de persistencia de gastos directos.</summary>
public interface IGastoDirectoRepository
{
    Task<IReadOnlyList<GastoDirecto>> ListarAsync(FiltroGastosDirectos filtro);
    Task<IReadOnlyList<CentroCostoGastoDirecto>> ListarCentrosCostoAsync();
    Task<IReadOnlyList<ProveedorGastoDirecto>> ListarProveedoresAsync();
    Task<IReadOnlyList<PartidaDisponibleGasto>> ListarPartidasDisponiblesAsync(int idCentroCosto);
    Task<(GastoDirecto? Gasto, IReadOnlyList<GastoDirectoDocumento> Documentos)> ObtenerAsync(int idGastoDirecto);
    Task<int> CrearAsync(RegistroGastoDirecto registro);
    Task<int> ActualizarAsync(int idGastoDirecto, RegistroGastoDirecto registro);
    Task ConfirmarAsync(int idGastoDirecto);
    Task AnularAsync(int idGastoDirecto);
    Task RegistrarDocumentosAsync(int idGastoDirecto, IReadOnlyCollection<DocumentoNuevo> documentos);
}
