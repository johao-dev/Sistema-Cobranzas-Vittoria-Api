using Cobranzas_Vittoria.Application.Importacion.Excepciones;

namespace Cobranzas_Vittoria.Application.Inventario.Excepciones;

/// <summary>
/// Excepcion de validacion de negocio especifica del modulo Inventario (Kardex manual).
///
/// <para>
/// <b>Por que extiende <see cref="DatosInvalidosException"/> y no crea una nueva</b>:
/// el <c>ApiExceptionMiddleware</c> ya captura <c>DatosInvalidosException</c> y la
/// mapea a HTTP 422 con la lista de errores por fila. Al extender esa clase
/// (en lugar de crear una excepcion paralela), el Inventario se beneficia del
/// mismo contrato HTTP sin necesidad de modificar el middleware.
/// </para>
///
/// <para>
/// <b>Por que existe (no basta con <c>DatosInvalidosException</c>)</b>:
///   1. Marker class para que el caller y el codigo de logging puedan
///      distinguir errores "del Inventario" de errores "de Importacion"
///      sin acoplarse al namespace.
///   2. Punto de extension futuro: si Inventario necesita campos extra
///      (ej: contexto del kardex que fallo), los agrega aqui sin tocar
///      la base de Importacion.
/// </para>
///
/// <para>
/// <b>Codigo por defecto</b>: <c>VALIDACION_NEGOCIO</c>. El codigo real
/// vive en cada <see cref="DetalleErrorFila.CodigoError"/>, no en la
/// excepcion padre, para mantener compatibilidad con el contrato del
/// middleware.
/// </para>
/// </summary>
public sealed class ValidacionNegocioInventarioException : DatosInvalidosException
{
    /// <summary>Codigo de error de la excepcion padre (sobrecarga del catch del middleware).</summary>
    public const string CodigoError = "VALIDACION_NEGOCIO";

    /// <summary>
    /// Crea una excepcion de validacion de Inventario a partir de una lista
    /// de <see cref="DetalleErrorFila"/> (los que devuelve
    /// <c>SqlExceptionTranslator</c> o construye el validador).
    /// </summary>
    public ValidacionNegocioInventarioException(IReadOnlyList<DetalleErrorFila> errores)
        : base($"La operacion de Kardex fue rechazada con {errores.Count} error(es) de validacion.", errores)
    {
    }

    /// <summary>
    /// Crea una excepcion de validacion de Inventario con un mensaje
    /// personalizado y un unico error.
    /// </summary>
    public ValidacionNegocioInventarioException(string mensaje, DetalleErrorFila error)
        : base(mensaje, new[] { error })
    {
    }
}
