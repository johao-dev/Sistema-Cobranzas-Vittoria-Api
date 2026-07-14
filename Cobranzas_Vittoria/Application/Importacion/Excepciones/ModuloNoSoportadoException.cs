namespace Cobranzas_Vittoria.Application.Importacion.Excepciones;

/// <summary>
/// El módulo solicitado no tiene un <c>IImportProcessor</c> registrado en DI.
/// Mapea a HTTP 400 (BadRequest) con codigo "MODULO_NO_SOPORTADO".
/// </summary>
public class ModuloNoSoportadoException : Exception
{
    public const string CodigoError = "MODULO_NO_SOPORTADO";

    public ModuloNoSoportadoException(string mensaje)
        : base(mensaje)
    {
    }
}
