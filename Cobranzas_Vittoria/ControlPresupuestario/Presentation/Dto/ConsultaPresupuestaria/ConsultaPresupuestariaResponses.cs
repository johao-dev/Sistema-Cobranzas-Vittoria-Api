using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Resumen;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.ConsultaPresupuestaria;

/// <summary>Saldo por partida (saldo y control vigente), con los nombres del contrato.</summary>
public sealed record SaldoPartidaResponse(int IdCentroCosto, string? CodigoCentroCosto, string? CentroCosto, int IdPresupuesto,
    string? CodigoPresupuesto, string? Presupuesto, int IdMoneda, string? Moneda, string? SimboloMoneda,
    int IdPresupuestoVersion, int NumeroVersion, string? Estado, int IdPresupuestoDetalle, int IdCatalogoPartida, string? CodigoPartida,
    string? Partida, int Nivel, decimal MontoPresupuestado, decimal Comprometido, decimal Ejecutado, decimal SaldoDisponible,
    decimal MontoExcedido, bool Excedido)
{
    public static SaldoPartidaResponse Desde(SaldoPartidaFila f) => new(f.IdCentroCosto, f.CodigoCentroCosto, f.NombreCentroCosto, f.IdPresupuesto,
        f.CodigoPresupuesto, f.NombrePresupuesto, f.IdMoneda, f.CodigoMoneda, f.SimboloMoneda, f.IdPresupuestoVersion,
        f.NumeroVersion, f.EstadoPresupuesto, f.IdPresupuestoDetalle, f.IdCatalogoPartida,
        f.CodigoPartida, f.NombrePartida, f.Nivel, f.MontoPresupuestado, f.MontoComprometido, f.MontoEjecutado,
        f.SaldoDisponible, f.MontoExcedido, f.Excedido);
}

public sealed record PresupuestoVsComprometidoResponse(int IdCentroCosto, string? CodigoCentroCosto, string? CentroCosto, int IdPresupuesto,
    string? CodigoPresupuesto, string? Presupuesto, int IdMoneda, string? Moneda, string? SimboloMoneda,
    int IdPresupuestoVersion, int NumeroVersion, string? Estado, int IdPresupuestoDetalle, int IdCatalogoPartida,
    string? CodigoPartida, string? Partida, int Nivel, decimal MontoPresupuestado, decimal Comprometido, decimal Diferencia,
    decimal PorcentajeComprometido)
{
    public static PresupuestoVsComprometidoResponse Desde(PresupuestoVsComprometidoFila f) => new(f.IdCentroCosto, f.CodigoCentroCosto, f.NombreCentroCosto, f.IdPresupuesto,
        f.CodigoPresupuesto, f.NombrePresupuesto, f.IdMoneda, f.CodigoMoneda, f.SimboloMoneda, f.IdPresupuestoVersion,
        f.NumeroVersion, f.EstadoPresupuesto,
        f.IdPresupuestoDetalle, f.IdCatalogoPartida, f.CodigoPartida, f.NombrePartida, f.Nivel, f.MontoPresupuestado,
        f.MontoComprometido, f.Diferencia, f.PorcentajeComprometido);
}

public sealed record PresupuestoVsEjecutadoResponse(int IdCentroCosto, string? CodigoCentroCosto, string? CentroCosto, int IdPresupuesto,
    string? CodigoPresupuesto, string? Presupuesto, int IdMoneda, string? Moneda, string? SimboloMoneda,
    int IdPresupuestoVersion, int NumeroVersion, string? Estado, int IdPresupuestoDetalle, int IdCatalogoPartida,
    string? CodigoPartida, string? Partida, int Nivel, decimal MontoPresupuestado, decimal Ejecutado, decimal Diferencia,
    decimal PorcentajeEjecutado)
{
    public static PresupuestoVsEjecutadoResponse Desde(PresupuestoVsEjecutadoFila f) => new(f.IdCentroCosto, f.CodigoCentroCosto, f.NombreCentroCosto, f.IdPresupuesto,
        f.CodigoPresupuesto, f.NombrePresupuesto, f.IdMoneda, f.CodigoMoneda, f.SimboloMoneda, f.IdPresupuestoVersion,
        f.NumeroVersion, f.EstadoPresupuesto,
        f.IdPresupuestoDetalle, f.IdCatalogoPartida, f.CodigoPartida, f.NombrePartida, f.Nivel, f.MontoPresupuestado,
        f.MontoEjecutado, f.Diferencia, f.PorcentajeEjecutado);
}

public sealed record GastoPorPartidaResponse(int IdCentroCosto, string? CodigoCentroCosto, string? CentroCosto, int IdPresupuesto,
    string? CodigoPresupuesto, string? Presupuesto, int IdMoneda, string? Moneda, string? SimboloMoneda,
    int IdPresupuestoVersion, int NumeroVersion, string? Estado, int IdCatalogoPartida, string? CodigoPartida, string? Partida, int Nivel,
    decimal MontoPresupuestado, decimal Ejecutado, decimal Diferencia, decimal PorcentajeEjecutado)
{
    public static GastoPorPartidaResponse Desde(GastoPorPartidaFila f) => new(f.IdCentroCosto, f.CodigoCentroCosto, f.NombreCentroCosto, f.IdPresupuesto,
        f.CodigoPresupuesto, f.NombrePresupuesto, f.IdMoneda, f.CodigoMoneda, f.SimboloMoneda, f.IdPresupuestoVersion,
        f.NumeroVersion, f.EstadoPresupuesto, f.IdCatalogoPartida,
        f.CodigoPartida, f.NombrePartida, f.Nivel, f.MontoPresupuestado, f.MontoEjecutado, f.Diferencia, f.PorcentajeEjecutado);
}

public sealed record GastoPorCentroCostoResponse(int IdCentroCosto, string? CodigoCentroCosto, string? CentroCosto, int IdPresupuesto,
    string? CodigoPresupuesto, string? Presupuesto, int IdMoneda, string? Moneda, string? SimboloMoneda,
    int IdPresupuestoVersion, int NumeroVersion, string? Estado, decimal MontoPresupuestado, decimal Comprometido, decimal Ejecutado,
    decimal SaldoDisponible, decimal PorcentajeEjecutado, bool Excedido)
{
    public static GastoPorCentroCostoResponse Desde(GastoPorCentroCostoFila f) => new(f.IdCentroCosto, f.CodigoCentroCosto, f.NombreCentroCosto, f.IdPresupuesto,
        f.CodigoPresupuesto, f.NombrePresupuesto, f.IdMoneda, f.CodigoMoneda, f.SimboloMoneda, f.IdPresupuestoVersion,
        f.NumeroVersion, f.EstadoPresupuesto, f.MontoPresupuestado,
        f.MontoComprometido, f.MontoEjecutado, f.SaldoDisponible, f.PorcentajeEjecutado, f.Excedido);
}

public sealed record ResumenResponse(ResumenTotales Totales, IReadOnlyList<GastoPorCentroCostoResponse> PorCentroCosto,
    IReadOnlyList<SaldoPartidaResponse> PartidasExcedidas)
{
    public static ResumenResponse Desde(ResumenResult r) => new(r.Totales,
        r.PorCentroCosto.Select(GastoPorCentroCostoResponse.Desde).ToList(),
        r.PartidasExcedidas.Select(SaldoPartidaResponse.Desde).ToList());
}
