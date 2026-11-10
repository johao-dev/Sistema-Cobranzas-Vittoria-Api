namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;

public sealed record CrearRequerimientoCommand(
    string? NumeroRequerimiento,
    DateOnly FechaRequerimiento,
    int IdProyecto,
    string? Descripcion,
    DateOnly? FechaEntrega,
    string? Observacion,
    IReadOnlyList<CrearItemRequerimientoCommand?>? Items);

public sealed record CrearItemRequerimientoCommand(
    int IdMaterial, int? IdPresupuestoDetalle, decimal Cantidad, string? Observacion);
