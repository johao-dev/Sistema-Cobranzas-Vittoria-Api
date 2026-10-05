namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

public sealed class SeccionGastoEntity
{
    public int IdSeccionGasto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public int Orden { get; set; }
    public string? CodigosTipoCentroCosto { get; set; }
}
