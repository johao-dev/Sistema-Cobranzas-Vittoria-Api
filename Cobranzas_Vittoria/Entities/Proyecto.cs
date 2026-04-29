namespace Cobranzas_Vittoria.Entities;
public class Proyecto
{
    public int IdProyecto { get; set; }
    public string NombreProyecto { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal CotizacionGeneral { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
}
