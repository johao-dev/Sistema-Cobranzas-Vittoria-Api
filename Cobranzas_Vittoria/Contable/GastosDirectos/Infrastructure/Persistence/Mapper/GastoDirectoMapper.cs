using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Persistence.Mapper;

public static class GastoDirectoMapper
{
    public static GastoDirecto ToDomain(GastoDirectoEntity e) => new()
    {
        IdGastoDirecto = e.IdGastoDirecto, IdPresupuestoDetalle = e.IdPresupuestoDetalle, IdProveedor = e.IdProveedor,
        Proveedor = e.Proveedor, IdMoneda = e.IdMoneda, Moneda = e.Moneda, Fecha = e.Fecha, Concepto = e.Concepto,
        Descripcion = e.Descripcion, Monto = e.Monto, Estado = e.Estado, FechaCreacion = e.FechaCreacion,
        FechaActualizacion = e.FechaActualizacion, IdPresupuesto = e.IdPresupuesto, CodigoPresupuesto = e.CodigoPresupuesto,
        IdCentroCosto = e.IdCentroCosto, CodigoCentroCosto = e.CodigoCentroCosto, CentroCosto = e.CentroCosto,
        IdProyecto = e.IdProyecto, IdCatalogoPartida = e.IdCatalogoPartida, CodigoPartida = e.CodigoPartida,
        Partida = e.Partida, TotalDocumentos = e.TotalDocumentos, CodigoSeccionGasto = e.CodigoSeccionGasto,
        NombreSeccionGasto = e.NombreSeccionGasto, IdMonedaOriginal = e.IdMonedaOriginal, MonedaOriginal = e.MonedaOriginal,
        MontoOriginal = e.MontoOriginal, TipoCambio = e.TipoCambio, FechaTipoCambio = e.FechaTipoCambio
    };

    public static GastoDirectoDocumento ToDomain(GastoDirectoDocumentoEntity e) => new(e.IdGastoDirectoDocumento,
        e.IdGastoDirecto, e.TipoDocumento, e.NombreArchivo, e.RutaArchivo, e.Extension, e.FechaCreacion);

    public static ProveedorSeccion ToDomain(ProveedorSeccionEntity e) => new(e.IdProveedor, e.RazonSocial, e.Ruc, e.DeLaSeccion);

    public static CentroCostoSeccion ToDomain(CentroCostoSeccionEntity e)
        => new(e.IdCentroCosto, e.Codigo, e.Nombre, e.IdProyecto, e.CodigoTipoCentroCosto);

    public static PartidaDisponibleGasto ToDomain(PartidaDisponibleGastoEntity e) => new(e.IdPresupuestoDetalle,
        e.IdPresupuesto, e.CodigoPresupuesto, e.IdPresupuestoVersion, e.NumeroVersion, e.IdCatalogoPartida, e.CodigoPartida,
        e.NombrePartida, e.IdMoneda, e.CodigoMoneda, e.SimboloMoneda, e.MontoPresupuestado, e.MontoComprometido,
        e.MontoEjecutado, e.SaldoDisponible);
}
