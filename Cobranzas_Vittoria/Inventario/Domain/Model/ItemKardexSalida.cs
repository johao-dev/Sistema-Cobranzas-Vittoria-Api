namespace Cobranzas_Vittoria.Inventario.Domain.Model;

/// <summary>Material, cantidad y observación de un detalle de salida.</summary>
public sealed class ItemKardexSalida
{
    public int IdMaterial { get; set; }

    public decimal Cantidad { get; set; }

    public string? Observacion { get; set; }
}
