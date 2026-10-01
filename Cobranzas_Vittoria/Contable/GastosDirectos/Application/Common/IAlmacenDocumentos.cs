namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Common;

/// <summary>Archivo recibido por un caso de uso, independiente de ASP.NET.</summary>
public sealed record ArchivoAdjunto(string NombreArchivo, byte[] Contenido);

/// <summary>Puerto del almacén físico de los PDF adjuntos a los gastos directos.</summary>
public interface IAlmacenDocumentos
{
    /// <summary>Guarda el archivo y devuelve su ruta relativa (la que se registra en la base).</summary>
    Task<string> GuardarAsync(int idGastoDirecto, string tipoDocumento, ArchivoAdjunto archivo);

    /// <summary>Compensación: borra un archivo guardado cuando falla el registro en la base.</summary>
    void Eliminar(string rutaRelativa);

    /// <summary>Ruta física de un documento, o null si no existe o sale del almacén.</summary>
    string? ResolverRuta(string rutaRelativa);
}
