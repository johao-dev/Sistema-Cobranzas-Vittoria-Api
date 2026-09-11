namespace Cobranzas_Vittoria.Dtos.Compras.Requerimientos;

public sealed class ActualizarCantidadesAlmacenRequest
{
    public List<ActualizarCantidadAlmacenItemRequest> Items { get; init; } = [];
}

public sealed class ActualizarCantidadAlmacenItemRequest
{
    public int IdRequerimientoDetalle { get; init; }
    public decimal Cantidad { get; init; }
}
