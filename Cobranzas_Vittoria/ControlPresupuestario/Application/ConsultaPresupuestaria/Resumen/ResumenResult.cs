using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Resumen;

public sealed record ResumenTotales(decimal Presupuestado, decimal Comprometido, decimal Ejecutado, decimal SaldoDisponible,
    decimal PorcentajeEjecutado, int CentrosCosto, int Partidas, int PartidasExcedidas);

/// <summary>Totales generales, totales por centro de costo y versión, y partidas excedidas.</summary>
public sealed record ResumenResult(ResumenTotales Totales, IReadOnlyList<GastoPorCentroCostoFila> PorCentroCosto,
    IReadOnlyList<SaldoPartidaFila> PartidasExcedidas);
