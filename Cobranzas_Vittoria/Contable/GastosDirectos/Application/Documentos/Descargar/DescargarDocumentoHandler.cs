using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Common;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Documentos.Descargar;

public sealed class DescargarDocumentoHandler
{
    private readonly IGastoDirectoRepository _repository;
    private readonly IAlmacenDocumentos _almacen;

    public DescargarDocumentoHandler(IGastoDirectoRepository repository, IAlmacenDocumentos almacen)
    {
        _repository = repository;
        _almacen = almacen;
    }

    public async Task<DescargarDocumentoResult> HandleAsync(DescargarDocumentoQuery query)
    {
        GastoDirectoValidator.ValidarId(query.IdGastoDirecto);
        var documento = (await _repository.ObtenerAsync(query.IdGastoDirecto)).Documentos
            .FirstOrDefault(d => d.IdGastoDirectoDocumento == query.IdGastoDirectoDocumento)
            ?? throw GastoDirectoNoEncontradoException.Documento(query.IdGastoDirectoDocumento);
        var ruta = _almacen.ResolverRuta(documento.RutaArchivo)
            ?? throw new GastoDirectoNoEncontradoException("No se encontró el archivo físico del documento.");
        return new DescargarDocumentoResult(ruta, documento.NombreArchivo);
    }
}
