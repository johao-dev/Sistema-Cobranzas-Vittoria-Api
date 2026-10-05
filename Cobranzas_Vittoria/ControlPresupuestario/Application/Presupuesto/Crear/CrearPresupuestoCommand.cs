namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto.Crear;

public sealed record CrearPresupuestoCommand(int IdCentroCosto, int IdMoneda, string Codigo, string Nombre,
    string? Descripcion, DateTime? FechaInicio, DateTime? FechaFin);
