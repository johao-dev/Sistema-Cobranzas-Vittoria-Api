namespace Cobranzas_Vittoria.Application.Importacion.Excepciones;

/// <summary>
/// Errores de validación de la ESTRUCTURA del archivo:
/// codificación inválida, encabezados incorrectos, formato del archivo inválido.
/// Mapea a HTTP 400 (BadRequest).
///
/// Codigos esperados:
///   - "CODIFICACION_INVALIDA"
///   - "ENCABEZADOS_INCORRECTOS"
///   - "FORMATO_INVALIDO"
/// </summary>
public class EstructuraInvalidaException : Exception
{
    public string Codigo { get; }

    public EstructuraInvalidaException(string codigo, string mensaje)
        : base(mensaje)
    {
        Codigo = codigo;
    }
}
