using Cobranzas_Vittoria.Application.Common.Excepciones;

namespace Cobranzas_Vittoria.Application.Compras.Excepciones;

/// <summary>Regla de negocio rechazada durante el flujo de requerimientos.</summary>
public sealed class ValidacionNegocioComprasException : DatosInvalidosValidacionException
{
    public ValidacionNegocioComprasException(string campo, string codigoError, string mensaje)
        : base("La operación de Compras fue rechazada.",
            [new DetalleErrorValidacion(null, campo, codigoError, mensaje)])
    {
    }
}
