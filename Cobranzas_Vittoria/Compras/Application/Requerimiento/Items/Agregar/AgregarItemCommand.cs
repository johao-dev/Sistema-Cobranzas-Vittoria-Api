namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Agregar;

public sealed record AgregarItemCommand(int IdRequerimiento, int IdMaterial, decimal Cantidad,
    int? IdPresupuestoDetalle, string? Observacion);
