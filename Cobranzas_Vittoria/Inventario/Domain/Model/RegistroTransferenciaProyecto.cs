namespace Cobranzas_Vittoria.Inventario.Domain.Model;

public sealed class RegistroTransferenciaProyecto
{
    public int IdProyectoOrigen { get; set; }
    public int IdProyectoDestino { get; set; }
    public DateOnly Fecha { get; set; }
    public string Observacion { get; set; } = string.Empty;
    public IReadOnlyList<ItemTransferenciaProyecto> Items { get; set; } = Array.Empty<ItemTransferenciaProyecto>();
}

public sealed class ItemTransferenciaProyecto
{
    public int IdMaterial { get; set; }
    public decimal Cantidad { get; set; }
}
