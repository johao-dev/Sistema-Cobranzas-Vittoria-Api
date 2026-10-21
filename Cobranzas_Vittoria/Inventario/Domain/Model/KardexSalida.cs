namespace Cobranzas_Vittoria.Inventario.Domain.Model;

/// <summary>Fila de una salida manual; repite la cabecera por cada detalle de material.</summary>
public sealed class KardexSalida
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
