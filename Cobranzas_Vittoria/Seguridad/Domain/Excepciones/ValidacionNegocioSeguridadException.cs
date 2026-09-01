using Cobranzas_Vittoria.Application.Common.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

/// <summary>
/// Excepcion de validacion de negocio especifica del modulo Seguridad.
/// Hereda de <see cref="DatosInvalidosValidacionException"/> para que el
/// <c>ApiExceptionMiddleware</c> la mapee automaticamente a HTTP 422.
/// </summary>
public sealed class ValidacionNegocioSeguridadException : DatosInvalidosValidacionException
{
    public const string CodigoError = "VALIDACION_SEGURIDAD";

    public ValidacionNegocioSeguridadException(IReadOnlyList<DetalleErrorValidacion> errores)
        : base($"La operacion de Seguridad fue rechazada con {errores.Count} error(es) de validacion.", errores)
    {
    }

    public ValidacionNegocioSeguridadException(string mensaje, DetalleErrorValidacion error)
        : base(mensaje, new[] { error })
    {
    }

    public ValidacionNegocioSeguridadException(string campo, string codigoError, string mensaje)
        : this("Se encontro un error de validacion en Seguridad.", new DetalleErrorValidacion(null, campo, codigoError, mensaje))
    {
    }
}
