namespace Cobranzas_Vittoria.Application.Importacion.Excepciones;

/// <summary>
/// El modulo solicitado en <c>GET /api/import/{modulo}/plantilla</c> no tiene
/// una plantilla v2 disponible.
///
/// <para>
/// Esto cubre DOS casos que se modelan juntos:
///   - El modulo no existe (<c>xyz</c>, <c>foo</c>).
///   - El modulo existe pero aun no se ha migrado al esquema v2 de importacion
///     (ej: <c>unidad-medida</c>, <c>especialidad</c>).
/// </para>
///
/// <para>
/// Mapea a HTTP 404 (NotFound) con codigo <c>"PLANTILLA_NO_DISPONIBLE"</c>.
/// Se diferencia de <see cref="ModuloNoSoportadoException"/> (que es 400 para
/// la operacion POST de importacion) en que la URL existe (no es un 404
/// generico de ruta) pero el recurso "plantilla" no esta disponible para ese
/// modulo.
/// </para>
/// </summary>
public class PlantillaNoDisponibleException : Exception
{
    public const string CodigoError = "PLANTILLA_NO_DISPONIBLE";

    public string Modulo { get; }

    public PlantillaNoDisponibleException(string modulo, string mensaje)
        : base(mensaje)
    {
        Modulo = modulo;
    }
}
