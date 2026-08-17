namespace Cobranzas_Vittoria.Application.Inventario.Dtos;

/// <summary>
/// DTO de salida que representa una salida de Kardex manual con todos sus
/// items. Se devuelve en GET (lista y detalle), POST y PUT de KardexSalida.
///
/// <para>
/// <b>Una fila por item</b>: el SP lista las salidas con un JOIN a
/// KardexSalidaDetalle, de modo que cada item produce una fila que repite
/// los datos de cabecera. Esto simplifica el render en tablas planas del
/// frontend sin necesidad de agrupar en cliente.
/// </para>
///
/// <para>
/// <b>Por que no usa una jerarquia Cabecera + Items</b>:
/// la convencion del proyecto para listados con detalle repetido es
/// aplanar en una sola respuesta (mismo patron que en KardexResumenMaterial).
/// El frontend agrupa por <c>IdKardexSalida</c> si quiere vista jerarquica.
/// </para>
///
/// <para>
/// <b>DetalleObservacion vs Observacion</b>:
///   - <see cref="Observacion"/> = observacion de la cabecera.
///   - <see cref="DetalleObservacion"/> = observacion del item especifico.
/// Mantener nombres explicitos evita confusion al consumir el JSON.
/// </para>
/// </summary>
public sealed class KardexSalidaResponseDto
{
    public int IdKardexSalida { get; set; }
    public int IdEspecialidad { get; set; }
    public string? Especialidad { get; set; }
    public int? IdProyecto { get; set; }
    public string? Proyecto { get; set; }
    public string? NumeroDocumento { get; set; }
    public DateOnly Fecha { get; set; }
    public string Solicitante { get; set; } = string.Empty;
    public string? Observacion { get; set; }

    // Campos del item (repetidos por cada fila devuelta por el SP).
    public int IdKardexSalidaDetalle { get; set; }
    public int IdMaterial { get; set; }
    public string? CodigoMaterial { get; set; }
    public string? Nombre { get; set; }
    public decimal Cantidad { get; set; }
    public string? DetalleObservacion { get; set; }

    public DateTime FechaCreacion { get; set; }
}
