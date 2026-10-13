namespace Cobranzas_Vittoria.Dtos.Compras.Requerimientos;

public sealed record AgregarItemRequerimientoRequest
{
    public int IdMaterial { get; init; }

    /*
        Tecnicamente no debería pedirse el IdPresupuestoDetalle ya que el material debería estar asociado a una
        partida presupuestal, pero como no se ha terminado la integración con el control presupuestario,
        se permite que el usuario indique el IdPresupuestoDetalle para poder asociar el item de requerimiento
        a la partida presupuestal correspondiente. Esto es temporal y se espera que en el futuro se pueda
        obtener el IdPresupuestoDetalle a partir del IdMaterial.
    */
    public int? IdPresupuestoDetalle { get; init; }
    public decimal Cantidad { get; init; }
    public string? Observacion { get; init; }
}