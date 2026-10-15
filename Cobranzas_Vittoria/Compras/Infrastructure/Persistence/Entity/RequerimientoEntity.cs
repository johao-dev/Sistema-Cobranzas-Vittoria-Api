namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Entity;

public class RequerimientoEntity
{
    public int IdRequerimiento { get; set; }
    public string NumeroRequerimiento { get; set; } = string.Empty;
    public DateOnly FechaRequerimiento { get; set; }
    public int IdProyecto { get; set; }
    public string? Descripcion { get; set; } = string.Empty;
    public DateOnly? FechaEntrega { get; set; }
    public int IdUsuarioSolicitante { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Observacion { get; set; }
    public DateTime FechaCreacion { get; set; }
}