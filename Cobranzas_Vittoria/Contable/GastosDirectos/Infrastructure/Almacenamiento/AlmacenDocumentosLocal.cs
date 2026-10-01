using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Common;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Almacenamiento;

/// <summary>
/// Adaptador del almacén de documentos sobre el disco del servidor:
/// wwwroot/uploads/gastos-directos/{id}/{factura|pago}/{guid}_{nombre}.pdf
/// </summary>
public sealed class AlmacenDocumentosLocal : IAlmacenDocumentos
{
    private readonly string _raiz;

    public AlmacenDocumentosLocal(IWebHostEnvironment environment)
        => _raiz = environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

    public async Task<string> GuardarAsync(int idGastoDirecto, string tipoDocumento, ArchivoAdjunto archivo)
    {
        var carpetaRelativa = Path.Combine("uploads", "gastos-directos", idGastoDirecto.ToString(), tipoDocumento.ToLowerInvariant());
        var carpeta = Path.Combine(_raiz, carpetaRelativa);
        Directory.CreateDirectory(carpeta);
        var nombreSeguro = $"{Guid.NewGuid():N}_{Path.GetFileName(archivo.NombreArchivo)}";
        await File.WriteAllBytesAsync(Path.Combine(carpeta, nombreSeguro), archivo.Contenido);
        return Path.Combine(carpetaRelativa, nombreSeguro).Replace("\\", "/");
    }

    public void Eliminar(string rutaRelativa)
    {
        var ruta = ResolverRuta(rutaRelativa);
        if (ruta is not null) File.Delete(ruta);
    }

    public string? ResolverRuta(string rutaRelativa)
    {
        var completa = Path.GetFullPath(Path.Combine(_raiz, rutaRelativa.Replace('/', Path.DirectorySeparatorChar)));
        var raiz = Path.GetFullPath(_raiz) + Path.DirectorySeparatorChar;
        return completa.StartsWith(raiz, StringComparison.Ordinal) && File.Exists(completa) ? completa : null;
    }
}
