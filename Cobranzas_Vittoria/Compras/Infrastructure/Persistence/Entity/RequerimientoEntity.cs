namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Entity;

public class RequerimientoEntity
{
    public int IdRequerimiento { get; set; }
    public string NumeroRequerimiento { get; set; } = string.Empty;
    public DateOnly FechaRequerimiento { get; set; }
    public int IdProyecto { get; set; }
    // Conservar NULL al materializar; no sustituirlo por texto vacío antes de ejecutar el mapper.
    public string? Descripcion { get; set; }
    public DateOnly? FechaEntrega { get; set; }
    public int IdUsuarioSolicitante { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Observacion { get; set; }
    public DateTime FechaCreacion { get; set; }
}
