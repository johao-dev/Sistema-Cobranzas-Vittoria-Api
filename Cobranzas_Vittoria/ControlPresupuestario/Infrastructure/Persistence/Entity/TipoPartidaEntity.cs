namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

public sealed class TipoPartidaEntity
{
    public int IdTipoPartida { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}
