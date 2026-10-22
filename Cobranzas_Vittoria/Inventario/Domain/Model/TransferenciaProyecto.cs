namespace Cobranzas_Vittoria.Inventario.Domain.Model;

/// <summary>Una fila por detalle, siguiendo el contrato de las salidas de Inventario.</summary>
public sealed class TransferenciaProyecto
{
    public int IdTransferencia { get; set; }
    public int IdProyectoOrigen { get; set; }
    public string ProyectoOrigen { get; set; } = string.Empty;
    public int IdProyectoDestino { get; set; }
    public string ProyectoDestino { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Observacion { get; set; } = string.Empty;
    public int IdTransferenciaProyectoDetalle { get; set; }
    public int IdMaterial { get; set; }
    public string CodigoMaterial { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public int IdEspecialidad { get; set; }
    public string Especialidad { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
}
