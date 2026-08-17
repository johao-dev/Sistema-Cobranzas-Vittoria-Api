using Cobranzas_Vittoria.Application.Common.Excepciones;

namespace Cobranzas_Vittoria.Application.Inventario.Excepciones;

/// <summary>
/// Excepcion de validacion de negocio especifica del modulo Inventario (Kardex manual).
///
/// <para>
/// <b>Jerarquia</b>: extiende <see cref="DatosInvalidosValidacionException"/>
/// (en <c>Application.Common.Excepciones</c>), no
/// <c>Importacion.Excepciones.DatosInvalidosException</c>. La razon:
/// <c>DetalleErrorFila</c> (de Importacion) modela errores de filas de
/// archivos CSV/XLSX con el campo <c>Fila</c> como <c>int</c> obligatorio.
/// En Inventario no aplica el concepto de fila de archivo: la validacion
/// es de un payload HTTP. Usar el modelo de Common evita mentir con
/// <c>Fila = 0</c> y mantiene la semantica correcta.
/// </para>
///
/// <para>
/// <b>Mapeo HTTP</b>: el <c>ApiExceptionMiddleware</c> captura
/// <see cref="DatosInvalidosValidacionException"/> y la mapea a HTTP 422
/// con la lista de errores. Asi Inventario se beneficia del mismo
/// contrato HTTP que Importacion sin acoplarse a su namespace ni a
/// sus tipos especificos de error.
/// </para>
///
/// <para>
/// <b>Por que existe como marker class</b>:
/// permite al codigo de logging y a futuros tests distinguir errores
/// "del Inventario" de errores "de Importacion" sin acoplarse a strings.
/// Tambien es punto de extension futuro: si Inventario necesita campos
/// extra (ej: contexto del kardex que fallo), los agrega aqui.
/// </para>
///
/// <para>
/// <b>Codigo por defecto</b>: <c>VALIDACION_NEGOCIO</c>. El codigo real
/// vive en cada <see cref="DetalleErrorValidacion.CodigoError"/>, no
/// en la excepcion padre, para mantener compatibilidad con el contrato
/// del middleware.
/// </para>
/// </summary>
public sealed class ValidacionNegocioInventarioException : DatosInvalidosValidacionException
{
    /// <summary>Codigo de error de la excepcion padre (sobrecarga del catch del middleware).</summary>
    public const string CodigoError = "VALIDACION_NEGOCIO";

    /// <summary>
    /// Crea una excepcion de validacion de Inventario a partir de una lista
    /// de <see cref="DetalleErrorValidacion"/> (los que devuelve
    /// <c>SqlExceptionTranslator</c> o construye el validador).
    /// </summary>
    public ValidacionNegocioInventarioException(IReadOnlyList<DetalleErrorValidacion> errores)
        : base($"La operacion de Kardex fue rechazada con {errores.Count} error(es) de validacion.", errores)
    {
    }

    /// <summary>
    /// Crea una excepcion de validacion de Inventario con un mensaje
    /// personalizado y un unico error.
    /// </summary>
    public ValidacionNegocioInventarioException(string mensaje, DetalleErrorValidacion error)
        : base(mensaje, new[] { error })
    {
    }
}
