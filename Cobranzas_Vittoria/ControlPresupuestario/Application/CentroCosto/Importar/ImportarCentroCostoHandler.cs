using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Importar;

/// <summary>Importación masiva de centros de costo (todo o nada). Los errores por fila se informan juntos (422).</summary>
public sealed class ImportarCentroCostoHandler
{
    private readonly IImportadorMaestros _importador;
    private readonly IUsuarioActualService _usuarioActual;

    public ImportarCentroCostoHandler(IImportadorMaestros importador, IUsuarioActualService usuarioActual)
    {
        _importador = importador;
        _usuarioActual = usuarioActual;
    }

    public Task<ResultadoImportacion> HandleAsync(ImportarCentroCostoCommand command, CancellationToken ct)
        => _importador.ImportarCentrosCostoAsync(command.Archivo, _usuarioActual.ObtenerUsuarioActual(), ct);
}
