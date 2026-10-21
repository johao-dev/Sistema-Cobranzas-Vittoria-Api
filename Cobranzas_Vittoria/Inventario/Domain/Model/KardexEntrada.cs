namespace Cobranzas_Vittoria.Inventario.Domain.Model;

/// <summary>Entrada manual con los datos descriptivos de material, especialidad, proveedor y proyecto.</summary>
public sealed class KardexEntrada
{
    public int IdKardexEntrada { get; set; }
    public int IdEspecialidad { get; set; }
    public string? Especialidad { get; set; }
    public int IdMaterial { get; set; }
    public string? CodigoMaterial { get; set; }
    public string? Nombre { get; set; }
    public int? IdProveedor { get; set; }
    public string? Proveedor { get; set; }
    public int? IdProyecto { get; set; }
    public string? Proyecto { get; set; }
    public string? NumeroDocumento { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Cantidad { get; set; }
    public string? Observacion { get; set; }
    public DateTime FechaCreacion { get; set; }
}
