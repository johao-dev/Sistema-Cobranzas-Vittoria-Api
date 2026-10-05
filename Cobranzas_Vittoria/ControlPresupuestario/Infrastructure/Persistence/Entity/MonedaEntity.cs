namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

public sealed class MonedaEntity
{
    public int IdMoneda { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Simbolo { get; set; }
    public bool Activo { get; set; }
}
