using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Common;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.ValueObject;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Documentos.Subir;

/// <summary>
/// Guarda los PDF en el almacén y registra su metadata en una transacción. Si falla el registro,
/// borra los archivos ya guardados para no dejar huérfanos (compensación local).
/// </summary>
public sealed class SubirDocumentosHandler
{
    private readonly IGastoDirectoRepository _repository;
    private readonly IAlmacenDocumentos _almacen;

    public SubirDocumentosHandler(IGastoDirectoRepository repository, IAlmacenDocumentos almacen)
    {
        _repository = repository;
        _almacen = almacen;
    }

    public async Task<int> HandleAsync(SubirDocumentosCommand command)
    {
        GastoDirectoValidator.ValidarId(command.IdGastoDirecto);
        if (command.Archivos.Count == 0) throw new ValidacionGastoDirectoException("Debe adjuntar al menos un PDF.");
        var tipo = TipoDocumentoGasto.Normalizar(command.TipoDocumento);
        if (command.Archivos.Any(a => !string.Equals(Path.GetExtension(a.NombreArchivo), ".pdf", StringComparison.OrdinalIgnoreCase)))
            throw new ValidacionGastoDirectoException("Solo se permiten archivos PDF.");

        var guardados = new List<DocumentoNuevo>();
        try
        {
            foreach (var archivo in command.Archivos)
            {
                var ruta = await _almacen.GuardarAsync(command.IdGastoDirecto, tipo, archivo);
                guardados.Add(new DocumentoNuevo(tipo, Path.GetFileName(archivo.NombreArchivo), ruta, ".pdf"));
            }
            await _repository.RegistrarDocumentosAsync(command.IdGastoDirecto, guardados);
            return guardados.Count;
        }
        catch
        {
            foreach (var documento in guardados) _almacen.Eliminar(documento.RutaArchivo);
            throw;
        }
    }
}
