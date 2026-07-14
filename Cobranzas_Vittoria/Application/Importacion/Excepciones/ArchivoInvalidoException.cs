namespace Cobranzas_Vittoria.Application.Importacion.Excepciones;

/// <summary>
/// Errores de validación del ARCHIVO en sí (no de su contenido):
/// extensión inválida, MIME inválido, tamaño excedido, archivo vacío.
/// Mapea a HTTP 400 (BadRequest) o 413 (Payload Too Large) según <see cref="Codigo"/>.
///
/// Codigos esperados:
///   - "EXTENSION_INVALIDA"
///   - "MIME_INVALIDO"
///   - "TAMANIO_EXCEDIDO"   -> 413
///   - "ARCHIVO_VACIO"
/// </summary>
public class ArchivoInvalidoException : Exception
{
    public string Codigo { get; }

    public ArchivoInvalidoException(string codigo, string mensaje)
        : base(mensaje)
    {
        Codigo = codigo;
    }
}
