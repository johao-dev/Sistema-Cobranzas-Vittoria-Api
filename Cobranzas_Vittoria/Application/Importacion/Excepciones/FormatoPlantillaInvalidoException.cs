namespace Cobranzas_Vittoria.Application.Importacion.Excepciones;

/// <summary>
/// El parametro <c>formato</c> del endpoint <c>GET /api/import/{modulo}/plantilla</c>
/// tiene un valor que no es <c>csv</c> ni <c>xlsx</c>.
///
/// <para>
/// Mapea a HTTP 400 (BadRequest) con codigo <c>"FORMATO_PLANTILLA_INVALIDO"</c>.
/// Es una excepcion especifica (no usamos <see cref="EstructuraInvalidaException"/>
/// que es para archivos) porque el formato de la plantilla es un parametro
/// de query string, no del archivo subido.
/// </para>
/// </summary>
public class FormatoPlantillaInvalidoException : Exception
{
    public const string CodigoError = "FORMATO_PLANTILLA_INVALIDO";

    public string FormatoRecibido { get; }

    public FormatoPlantillaInvalidoException(string formatoRecibido, string mensaje)
        : base(mensaje)
    {
        FormatoRecibido = formatoRecibido;
    }
}
