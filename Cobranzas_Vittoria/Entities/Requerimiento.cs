namespace Cobranzas_Vittoria.Entities;
public class Requerimiento
{
    public int IdRequerimiento { get; set; }
    public string NumeroRequerimiento { get; set; } = string.Empty;
    public DateTime FechaRequerimiento { get; set; }
    public string? Especialidades { get; set; }
    public int IdProyecto { get; set; }
    public string? NombreProyecto { get; set; }
    public string? Descripcion { get; set; }
    public DateTime? FechaEntrega { get; set; }
    public int IdUsuarioSolicitante { get; set; }
    public string? Solicitante { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Observacion { get; set; }
    public DateTime FechaCreacion { get; set; }
}
