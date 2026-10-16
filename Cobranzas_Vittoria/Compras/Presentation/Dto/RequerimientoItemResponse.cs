using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items;

namespace Cobranzas_Vittoria.Compras.Presentation.Dto;

public sealed record RequerimientoItemResponse(int IdRequerimientoDetalle, int IdRequerimiento, int IdMaterial,
    decimal Cantidad, int? IdPresupuestoDetalle, string? Observacion)
{
    public static RequerimientoItemResponse Desde(RequerimientoItemResult result)
        => new(result.IdRequerimientoDetalle, result.IdRequerimiento, result.IdMaterial, result.Cantidad,
            result.IdPresupuestoDetalle, result.Observacion);
}
