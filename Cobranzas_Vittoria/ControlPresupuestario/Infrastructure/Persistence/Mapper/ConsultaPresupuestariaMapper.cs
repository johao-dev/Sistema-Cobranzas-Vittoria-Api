using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;

/// <summary>Mapeo de las filas de las vistas de reporting a los modelos de lectura del dominio.</summary>
public static class ConsultaPresupuestariaMapper
{
    public static PresupuestoResumenFila ToDomain(PresupuestoResumenEntity e) => new()
    {
        IdCentroCosto = e.IdCentroCosto,
        CodigoCentroCosto = e.CodigoCentroCosto,
        NombreCentroCosto = e.NombreCentroCosto,
        IdPresupuesto = e.IdPresupuesto,
        CodigoPresupuesto = e.CodigoPresupuesto,
        NombrePresupuesto = e.NombrePresupuesto,
        IdMoneda = e.IdMoneda,
        CodigoMoneda = e.CodigoMoneda,
        SimboloMoneda = e.SimboloMoneda,
        IdPresupuestoVersion = e.IdPresupuestoVersion,
        NumeroVersion = e.NumeroVersion,
        EstadoPresupuesto = e.EstadoPresupuesto,
        IdPresupuestoDetalle = e.IdPresupuestoDetalle,
        IdCatalogoPartida = e.IdCatalogoPartida,
        CodigoPartida = e.CodigoPartida,
        NombrePartida = e.NombrePartida,
        Nivel = e.Nivel,
        MontoPresupuestado = e.MontoPresupuestado,
        MontoComprometido = e.MontoComprometido,
        MontoEjecutado = e.MontoEjecutado,
        SaldoDisponible = e.SaldoDisponible
    };

    public static PresupuestoVsComprometidoFila ToDomain(PresupuestoVsComprometidoEntity e) => new()
    {
        IdCentroCosto = e.IdCentroCosto,
        CodigoCentroCosto = e.CodigoCentroCosto,
        NombreCentroCosto = e.NombreCentroCosto,
        IdPresupuesto = e.IdPresupuesto,
        CodigoPresupuesto = e.CodigoPresupuesto,
        NombrePresupuesto = e.NombrePresupuesto,
        IdMoneda = e.IdMoneda,
        CodigoMoneda = e.CodigoMoneda,
        SimboloMoneda = e.SimboloMoneda,
        IdPresupuestoVersion = e.IdPresupuestoVersion,
        NumeroVersion = e.NumeroVersion,
        EstadoPresupuesto = e.EstadoPresupuesto,
        IdPresupuestoDetalle = e.IdPresupuestoDetalle,
        IdCatalogoPartida = e.IdCatalogoPartida,
        CodigoPartida = e.CodigoPartida,
        NombrePartida = e.NombrePartida,
        Nivel = e.Nivel,
        MontoPresupuestado = e.MontoPresupuestado,
        MontoComprometido = e.MontoComprometido,
        Diferencia = e.DiferenciaPresupuestoComprometido,
        PorcentajeComprometido = e.PorcentajeComprometido
    };

    public static PresupuestoVsEjecutadoFila ToDomain(PresupuestoVsEjecutadoEntity e) => new()
    {
        IdCentroCosto = e.IdCentroCosto,
        CodigoCentroCosto = e.CodigoCentroCosto,
        NombreCentroCosto = e.NombreCentroCosto,
        IdPresupuesto = e.IdPresupuesto,
        CodigoPresupuesto = e.CodigoPresupuesto,
        NombrePresupuesto = e.NombrePresupuesto,
        IdMoneda = e.IdMoneda,
        CodigoMoneda = e.CodigoMoneda,
        SimboloMoneda = e.SimboloMoneda,
        IdPresupuestoVersion = e.IdPresupuestoVersion,
        NumeroVersion = e.NumeroVersion,
        EstadoPresupuesto = e.EstadoPresupuesto,
        IdPresupuestoDetalle = e.IdPresupuestoDetalle,
        IdCatalogoPartida = e.IdCatalogoPartida,
        CodigoPartida = e.CodigoPartida,
        NombrePartida = e.NombrePartida,
        Nivel = e.Nivel,
        MontoPresupuestado = e.MontoPresupuestado,
        MontoEjecutado = e.MontoEjecutado,
        Diferencia = e.DiferenciaPresupuestoEjecutado,
        PorcentajeEjecutado = e.PorcentajeEjecutado
    };

    public static SaldoPartidaFila ToDomain(SaldoPresupuestarioEntity e) => new()
    {
        IdCentroCosto = e.IdCentroCosto,
        CodigoCentroCosto = e.CodigoCentroCosto,
        NombreCentroCosto = e.NombreCentroCosto,
        IdPresupuesto = e.IdPresupuesto,
        CodigoPresupuesto = e.CodigoPresupuesto,
        NombrePresupuesto = e.NombrePresupuesto,
        IdMoneda = e.IdMoneda,
        CodigoMoneda = e.CodigoMoneda,
        SimboloMoneda = e.SimboloMoneda,
        IdPresupuestoVersion = e.IdPresupuestoVersion,
        NumeroVersion = e.NumeroVersion,
        EstadoPresupuesto = e.EstadoPresupuesto,
        IdPresupuestoDetalle = e.IdPresupuestoDetalle,
        IdCatalogoPartida = e.IdCatalogoPartida,
        CodigoPartida = e.CodigoPartida,
        NombrePartida = e.NombrePartida,
        Nivel = e.Nivel,
        MontoPresupuestado = e.MontoPresupuestado,
        MontoComprometido = e.MontoComprometido,
        MontoEjecutado = e.MontoEjecutado,
        SaldoDisponible = e.SaldoDisponible,
        MontoExcedido = e.MontoExcedido,
        Excedido = e.Excedido
    };

    public static SaldoPartidaFila ToDomain(ControlPresupuestarioVigenteEntity e) => new()
    {
        IdCentroCosto = e.IdCentroCosto,
        CodigoCentroCosto = e.CodigoCentroCosto,
        NombreCentroCosto = e.NombreCentroCosto,
        IdPresupuesto = e.IdPresupuesto,
        CodigoPresupuesto = e.CodigoPresupuesto,
        NombrePresupuesto = e.NombrePresupuesto,
        IdMoneda = e.IdMoneda,
        CodigoMoneda = e.CodigoMoneda,
        SimboloMoneda = e.SimboloMoneda,
        IdPresupuestoVersion = e.IdPresupuestoVersion,
        NumeroVersion = e.NumeroVersion,
        EstadoPresupuesto = e.EstadoPresupuesto,
        IdPresupuestoDetalle = e.IdPresupuestoDetalle,
        IdCatalogoPartida = e.IdCatalogoPartida,
        CodigoPartida = e.CodigoPartida,
        NombrePartida = e.NombrePartida,
        Nivel = e.Nivel,
        MontoPresupuestado = e.MontoPresupuestado,
        MontoComprometido = e.MontoComprometido,
        MontoEjecutado = e.MontoEjecutado,
        SaldoDisponible = e.SaldoDisponible,
        MontoExcedido = e.MontoExcedido,
        Excedido = e.Excedido
    };

    public static GastoPorPartidaFila ToDomain(GastoPorPartidaEntity e) => new()
    {
        IdCentroCosto = e.IdCentroCosto,
        CodigoCentroCosto = e.CodigoCentroCosto,
        NombreCentroCosto = e.NombreCentroCosto,
        IdPresupuesto = e.IdPresupuesto,
        CodigoPresupuesto = e.CodigoPresupuesto,
        NombrePresupuesto = e.NombrePresupuesto,
        IdMoneda = e.IdMoneda,
        CodigoMoneda = e.CodigoMoneda,
        SimboloMoneda = e.SimboloMoneda,
        IdPresupuestoVersion = e.IdPresupuestoVersion,
        NumeroVersion = e.NumeroVersion,
        EstadoPresupuesto = e.EstadoPresupuesto,
        IdCatalogoPartida = e.IdCatalogoPartida,
        CodigoPartida = e.CodigoPartida,
        NombrePartida = e.NombrePartida,
        Nivel = e.Nivel,
        MontoPresupuestado = e.MontoPresupuestado,
        MontoEjecutado = e.MontoEjecutado,
        Diferencia = e.Diferencia,
        PorcentajeEjecutado = e.PorcentajeEjecutado
    };

    public static GastoPorCentroCostoFila ToDomain(GastoPorCentroCostoEntity e) => new()
    {
        IdCentroCosto = e.IdCentroCosto,
        CodigoCentroCosto = e.CodigoCentroCosto,
        NombreCentroCosto = e.NombreCentroCosto,
        IdPresupuesto = e.IdPresupuesto,
        CodigoPresupuesto = e.CodigoPresupuesto,
        NombrePresupuesto = e.NombrePresupuesto,
        IdMoneda = e.IdMoneda,
        CodigoMoneda = e.CodigoMoneda,
        SimboloMoneda = e.SimboloMoneda,
        IdPresupuestoVersion = e.IdPresupuestoVersion,
        NumeroVersion = e.NumeroVersion,
        EstadoPresupuesto = e.EstadoPresupuesto,
        MontoPresupuestado = e.MontoPresupuestado,
        MontoComprometido = e.MontoComprometido,
        MontoEjecutado = e.MontoEjecutado,
        SaldoDisponible = e.SaldoDisponible,
        PorcentajeEjecutado = e.PorcentajeEjecutado,
        Excedido = e.Excedido
    };

    public static EjecucionDiariaFila ToDomain(EjecucionDiariaEntity e)
        => new(e.IdPresupuesto, e.IdCatalogoPartida, e.Fecha, e.MontoEjecutado);
}
