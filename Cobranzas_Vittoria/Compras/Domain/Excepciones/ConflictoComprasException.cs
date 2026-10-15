namespace Cobranzas_Vittoria.Compras.Domain.Excepciones;

/// <summary>El estado actual del requerimiento o de sus ítems impide la operación.</summary>
public sealed class ConflictoComprasException : ComprasException
{
    public ConflictoComprasException(string codigoError, string mensaje)
        : base(codigoError, mensaje) { }
}
