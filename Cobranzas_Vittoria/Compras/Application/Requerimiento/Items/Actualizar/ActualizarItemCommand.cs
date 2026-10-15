using Cobranzas_Vittoria.Compras.Domain.ValueObject;

namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Actualizar;

public sealed record ActualizarItemCommand(int IdRequerimiento, int IdDetalle, decimal? Cantidad,
    int? IdPresupuestoDetalle, string? Observacion, CamposItemRequerimiento Campos);
