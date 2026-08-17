namespace Cobranzas_Vittoria.Application.Common.Excepciones;

/// <summary>
/// Excepcion generica para errores de validacion de API o de reglas de negocio.
/// Mapea a HTTP 422 (Unprocessable Entity) e incluye el detalle por
/// <see cref="DetalleErrorValidacion"/>.
///
/// <para>
/// <b>Por que existe ademas de <c>Importacion.Excepciones.DatosInvalidosException</c></b>:
/// la excepcion de Importacion modela errores de archivos CSV/XLSX
/// (su lista de errores es <c>IReadOnlyList&lt;DetalleErrorFila&gt;</c>,
/// donde <c>Fila</c> es <c>int</c>). Para payloads HTTP (DTOs de Kardex,
/// etc) queremos una excepcion con un modelo de error que tenga
/// <c>Fila</c> opcional (<c>int?</c>). Asi un error de validacion de
/// payload puede tener <c>Fila = null</c> (no hay fila de archivo),
/// sin tener que mentir con <c>Fila = 0</c>.
/// </para>
///
/// <para>
/// <b>Por que no se mueve/duplica la logica del middleware</b>:
/// el <c>ApiExceptionMiddleware</c> ya maneja el caso 422 para
/// <c>Importacion.Excepciones.DatosInvalidosException</c>. Esta clase
/// requiere una nueva entrada en la cadena de catch (aditiva, no destructiva)
/// que produce exactamente la misma respuesta JSON. Asi los dos tipos
/// coexisten sin acoplarse entre si.
/// </para>
///
/// <para>
/// <b>Por que es <c>abstract</c></b>:
/// el dominio rara vez lanzara esta clase directamente. Se espera que los
/// modulos definan sus propios tipos especificos (ej: Kardex lanza
/// <c>ValidacionNegocioInventarioException</c> que hereda de esta).
/// Marcar la base como abstracta evita lanzar "validacion generica" sin
/// contexto de modulo.
/// </para>
/// </summary>
public abstract class DatosInvalidosValidacionException : Exception
{
    /// <summary>Lista de errores de validacion que el cliente debe mostrar/corregir.</summary>
    public IReadOnlyList<DetalleErrorValidacion> Errores { get; }

    protected DatosInvalidosValidacionException(string mensaje, IReadOnlyList<DetalleErrorValidacion> errores)
        : base(mensaje)
    {
        if (errores is null) throw new ArgumentNullException(nameof(errores));
        if (errores.Count == 0)
            throw new ArgumentException("La lista de errores no puede estar vacia.", nameof(errores));
        Errores = errores;
    }
}
