namespace Cobranzas_Vittoria.Compras.Domain.Excepciones;

/// <summary>Colisión detectada antes de insertar; la transacción del alta fue revertida.</summary>
public sealed class NumeroRequerimientoOcupadoException : ComprasException
{
    public NumeroRequerimientoOcupadoException()
        : base("REQUERIMIENTO_NUMERO_OCUPADO", "El número fue ocupado por otra operación. Vuelve a intentar crear el requerimiento.") { }
}
