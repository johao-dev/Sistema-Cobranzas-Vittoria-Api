using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Importar;

/// <summary>Importación masiva de partidas del catálogo (todo o nada). Los errores por fila se informan juntos (422).</summary>
public sealed class ImportarCatalogoPartidaHandler
{
    private readonly IImportadorMaestros _importador;
    private readonly IUsuarioActualService _usuarioActual;

    public ImportarCatalogoPartidaHandler(IImportadorMaestros importador, IUsuarioActualService usuarioActual)
    {
        _importador = importador;
        _usuarioActual = usuarioActual;
    }

    public Task<ResultadoImportacion> HandleAsync(ImportarCatalogoPartidaCommand command, CancellationToken ct)
        => _importador.ImportarPartidasAsync(command.Archivo, _usuarioActual.ObtenerUsuarioActual(), ct);
}
