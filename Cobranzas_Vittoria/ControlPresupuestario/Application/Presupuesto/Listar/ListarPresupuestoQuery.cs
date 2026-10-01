namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto.Listar;

public sealed record ListarPresupuestoQuery(bool? Activo, int? IdCentroCosto, int? IdMoneda, string? Busqueda);
