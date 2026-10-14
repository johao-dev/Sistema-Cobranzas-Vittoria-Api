namespace Cobranzas_Vittoria.Application.Importacion.Excepciones;

/// <summary>
/// Errores de validación de DATOS o de REGLAS DE NEGOCIO dentro del archivo importado:
/// campos obligatorios vacíos, tipo de dato incorrecto, longitud excedida,
/// valor fuera de rango, ID de entidad inexistente.
/// Mapea a HTTP 422 (Unprocessable Entity) e incluye el detalle por fila.
///
/// Aplica cuando la validación se realiza en la API (no en el SP).
/// Si el SP lanza un error equivalente, el servicio lo captura y lo relanza
/// como esta misma excepción para mantener un único contrato HTTP.
/// </summary>
public class DatosInvalidosException : Exception
{
    public IReadOnlyList<DetalleErrorFila> Errores { get; }
    /// <summary>Resumen opcional de importaciones que cuentan filas físicas del lote.</summary>
    public int? TotalFilas { get; }

    public DatosInvalidosException(string mensaje, IReadOnlyList<DetalleErrorFila> errores, int? totalFilas = null)
        : base(mensaje)
    {
        Errores = errores;
        TotalFilas = totalFilas;
    }
}
