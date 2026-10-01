using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.Application.Importacion.Validators;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Importacion;

/// <summary>Adaptador del puerto IImportadorMaestros sobre los procesadores del motor de importación.</summary>
public sealed class ImportadorMaestros : IImportadorMaestros
{
    private readonly FileValidator _validador;
    private readonly CentroCostoImportProcessor _centrosCosto;
    private readonly CatalogoPartidaImportProcessor _partidas;

    public ImportadorMaestros(FileValidator validador, CentroCostoImportProcessor centrosCosto,
        CatalogoPartidaImportProcessor partidas)
    {
        _validador = validador;
        _centrosCosto = centrosCosto;
        _partidas = partidas;
    }

    public async Task<ResultadoImportacion> ImportarCentrosCostoAsync(ArchivoTabular archivo, string usuario, CancellationToken ct)
    {
        var formulario = ArchivoFormulario.Crear(archivo);
        _validador.Validar(formulario);
        var r = await _centrosCosto.EjecutarAsync(formulario, usuario, ct);
        return new ResultadoImportacion(r.Modulo, r.Formato, r.FilasInsertadas);
    }

    public async Task<ResultadoImportacion> ImportarPartidasAsync(ArchivoTabular archivo, string usuario, CancellationToken ct)
    {
        var formulario = ArchivoFormulario.Crear(archivo);
        _validador.Validar(formulario);
        var r = await _partidas.EjecutarAsync(formulario, usuario, ct);
        return new ResultadoImportacion(r.Modulo, r.Formato, r.FilasInsertadas);
    }
}
