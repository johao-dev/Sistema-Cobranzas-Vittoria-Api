using Cobranzas_Vittoria.Compras.Domain.Model;

namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Items;

public sealed record RequerimientoItemResult(int IdRequerimientoDetalle, int IdRequerimiento, int IdMaterial,
    decimal Cantidad, int? IdPresupuestoDetalle, string? Observacion)
{
    public static RequerimientoItemResult Desde(ItemRequerimiento item)
        => new(item.IdRequerimientoDetalle, item.IdRequerimiento, item.IdMaterial, item.Cantidad,
            item.IdPresupuestoDetalle, item.Observacion);
}
