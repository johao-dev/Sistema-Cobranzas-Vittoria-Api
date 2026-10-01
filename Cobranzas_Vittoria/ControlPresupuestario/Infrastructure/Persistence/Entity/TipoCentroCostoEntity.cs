namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

public sealed class TipoCentroCostoEntity
{
    public int IdTipoCentroCosto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}
