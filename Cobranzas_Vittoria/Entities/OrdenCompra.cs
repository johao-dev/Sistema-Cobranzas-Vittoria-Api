namespace Cobranzas_Vittoria.Entities;
public class OrdenCompra
{
    public int IdOrdenCompra { get; set; }
    public string NumeroOrdenCompra { get; set; } = string.Empty;
    public int IdRequerimiento { get; set; }
    public string? NumeroRequerimiento { get; set; }
    public int IdProveedor { get; set; }
    public string? Proveedor { get; set; }
    public string? Especialidad { get; set; }
    public string? Especialidades { get; set; }
    public int IdProyecto { get; set; }
    public string? NombreProyecto { get; set; }
    public DateTime FechaOrdenCompra { get; set; }
    public string? Descripcion { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string? RutaPdf { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int? IdUsuarioCreacion { get; set; }
}
