namespace Cobranzas_Vittoria.Application.Compras.Excepciones;

/// <summary>Indica que el estado actual de un recurso impide la operación solicitada.</summary>
public sealed class ConflictoNegocioComprasException : Exception
{
    public string CodigoError { get; }

    public ConflictoNegocioComprasException(string codigoError, string mensaje)
        : base(mensaje)
    {
        CodigoError = codigoError;
    }
}
