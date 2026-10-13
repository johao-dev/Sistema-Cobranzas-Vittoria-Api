namespace Cobranzas_Vittoria.Dtos.Compras.Requerimientos;

public sealed record ActualizarItemRequerimientoRequest
{
    public int? IdMaterial { get; init; } // hm, quizá no sea necesario, para seguir el enfoque de agregar y luego quitar.
    public int? IdPresupuestoDetalle { get; init; }
    public decimal? Cantidad { get; init; }
    public string? Observacion { get; init; }
}