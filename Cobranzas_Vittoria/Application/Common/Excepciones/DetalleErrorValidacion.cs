namespace Cobranzas_Vittoria.Application.Common.Excepciones;

/// <summary>
/// Detalle de un error de validacion a nivel de API o de regla de negocio.
/// Se serializa dentro de la respuesta 422 que produce
/// <see cref="DatosInvalidosValidacionException"/>.
///
/// <para>
/// <b>Por que existe (no alcanza con <c>Importacion.Excepciones.DetalleErrorFila</c>)</b>:
/// <c>DetalleErrorFila</c> modela errores por fila de un archivo CSV/XLSX
/// (el campo <c>Fila</c> es el numero de linea del archivo). Esa semantica
/// NO aplica a la validacion de payloads HTTP (DTOs de Kardex, etc), donde
/// no hay "fila de archivo" sino "campo del DTO". Esta clase:
///   - Hace <c>Fila</c> nullable para que sea opt-in (null en payloads HTTP,
///     valor numerico en archivos).
///   - Vive en <c>Application.Common</c> para que cualquier modulo la reuse.
///   - Mantiene la misma forma de serializacion JSON que el cliente ya
///     consume (campos <c>fila</c>, <c>campo</c>, <c>codigoError</c>,
///     <c>mensaje</c>), asi no rompe contratos.
/// </para>
///
/// <para>
/// <b>Por que <c>Fila</c> es <c>int?</c> y no <c>int</c> con default 0</b>:
/// el default 0 miente: no existe fila 0 en un archivo, y en una validacion
/// de payload no existe el concepto. Un null explicito es honesto sobre
/// "este error no esta asociado a una fila" y permite al cliente omitir
/// el campo si quiere.
/// </para>
///
/// <para>
/// <b>Relacion con <c>Importacion.Excepciones.DetalleErrorFila</c></b>:
/// esa clase NO se elimina ni se modifica (regla "no tocar lo existente").
/// Si en una fase futura se quiere unificar, se hara refactorizando
/// <c>DetalleErrorFila</c> para que herede de esta o para que ambos tipos
/// implementen una interfaz comun.
/// </para>
/// </summary>
public sealed record DetalleErrorValidacion(
    int? Fila,
    string Campo,
    string CodigoError,
    string Mensaje);
