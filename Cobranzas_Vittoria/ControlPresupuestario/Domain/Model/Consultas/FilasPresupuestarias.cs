namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;

/// <summary>Datos de cabecera que comparten todas las filas de las vistas presupuestarias.</summary>
public abstract record FilaPresupuestaria
{
    public int IdCentroCosto { get; init; }
    public string? CodigoCentroCosto { get; init; }
    public string? NombreCentroCosto { get; init; }
    public int IdPresupuesto { get; init; }
    public string? CodigoPresupuesto { get; init; }
    public string? NombrePresupuesto { get; init; }
    public int IdMoneda { get; init; }
    public string? CodigoMoneda { get; init; }
    public string? SimboloMoneda { get; init; }
    public int IdPresupuestoVersion { get; init; }
    public int NumeroVersion { get; init; }
    public string? EstadoPresupuesto { get; init; }
}

/// <summary>Fila por partida de versión (vw_PresupuestoResumen): base de las demás vistas.</summary>
public sealed record PresupuestoResumenFila : FilaPresupuestaria
{
    public int IdPresupuestoDetalle { get; init; }
    public int IdCatalogoPartida { get; init; }
    public string? CodigoPartida { get; init; }
    public string? NombrePartida { get; init; }
    public int Nivel { get; init; }
    public decimal MontoPresupuestado { get; init; }
    public decimal MontoComprometido { get; init; }
    public decimal MontoEjecutado { get; init; }
    public decimal SaldoDisponible { get; init; }
}

/// <summary>Saldo por partida (vw_Saldo por versión, vw_ControlPresupuestarioVigente para la versión vigente).</summary>
public sealed record SaldoPartidaFila : FilaPresupuestaria
{
    public int IdPresupuestoDetalle { get; init; }
    public int IdCatalogoPartida { get; init; }
    public string? CodigoPartida { get; init; }
    public string? NombrePartida { get; init; }
    public int Nivel { get; init; }
    public decimal MontoPresupuestado { get; init; }
    public decimal MontoComprometido { get; init; }
    public decimal MontoEjecutado { get; init; }
    public decimal SaldoDisponible { get; init; }
    public decimal MontoExcedido { get; init; }
    public bool Excedido { get; init; }
}

public sealed record PresupuestoVsComprometidoFila : FilaPresupuestaria
{
    public int IdPresupuestoDetalle { get; init; }
    public int IdCatalogoPartida { get; init; }
    public string? CodigoPartida { get; init; }
    public string? NombrePartida { get; init; }
    public int Nivel { get; init; }
    public decimal MontoPresupuestado { get; init; }
    public decimal MontoComprometido { get; init; }
    public decimal Diferencia { get; init; }
    public decimal PorcentajeComprometido { get; init; }
}

public sealed record PresupuestoVsEjecutadoFila : FilaPresupuestaria
{
    public int IdPresupuestoDetalle { get; init; }
    public int IdCatalogoPartida { get; init; }
    public string? CodigoPartida { get; init; }
    public string? NombrePartida { get; init; }
    public int Nivel { get; init; }
    public decimal MontoPresupuestado { get; init; }
    public decimal MontoEjecutado { get; init; }
    public decimal Diferencia { get; init; }
    public decimal PorcentajeEjecutado { get; init; }
}

/// <summary>Ejecución agregada por partida y versión (vw_GastosPorPartida).</summary>
public sealed record GastoPorPartidaFila : FilaPresupuestaria
{
    public int IdCatalogoPartida { get; init; }
    public string? CodigoPartida { get; init; }
    public string? NombrePartida { get; init; }
    public int Nivel { get; init; }
    public decimal MontoPresupuestado { get; init; }
    public decimal MontoEjecutado { get; init; }
    public decimal Diferencia { get; init; }
    public decimal PorcentajeEjecutado { get; init; }
}

/// <summary>Totales por centro de costo y versión (vw_GastosPorCentroCosto).</summary>
public sealed record GastoPorCentroCostoFila : FilaPresupuestaria
{
    public decimal MontoPresupuestado { get; init; }
    public decimal MontoComprometido { get; init; }
    public decimal MontoEjecutado { get; init; }
    public decimal SaldoDisponible { get; init; }
    public decimal PorcentajeEjecutado { get; init; }
    public bool Excedido { get; init; }
}

/// <summary>Efecto neto sobre lo ejecutado de una partida en un día (vw_EjecucionDiariaPorPartida).</summary>
public sealed record EjecucionDiariaFila(int IdPresupuesto, int IdCatalogoPartida, DateTime Fecha, decimal MontoEjecutado);
