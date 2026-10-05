namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

/// <summary>Resultado de una importación masiva de maestros (todo o nada).</summary>
public sealed record ResultadoImportacion(string Modulo, string Formato, int FilasInsertadas);

/// <summary>
/// Puerto de importación masiva de los maestros del módulo. El adaptador valida cada fila,
/// informa todos los errores juntos (422) y persiste con el SP de carga masiva del maestro.
/// </summary>
public interface IImportadorMaestros
{
    Task<ResultadoImportacion> ImportarCentrosCostoAsync(ArchivoTabular archivo, string usuario, CancellationToken ct);
    Task<ResultadoImportacion> ImportarPartidasAsync(ArchivoTabular archivo, string usuario, CancellationToken ct);
}
