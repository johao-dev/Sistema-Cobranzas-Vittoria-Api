namespace Cobranzas_Vittoria.Compras.Domain.Excepciones;

/// <summary>Base de los errores de dominio de Compras, identificados por un código estable.</summary>
public class ComprasException : Exception
{
    public string CodigoError { get; }

    public ComprasException(string codigoError, string mensaje)
        : base(mensaje) => CodigoError = codigoError;
}
