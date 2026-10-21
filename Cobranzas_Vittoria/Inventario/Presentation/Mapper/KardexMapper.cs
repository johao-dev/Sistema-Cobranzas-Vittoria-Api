using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Presentation.Dto;

namespace Cobranzas_Vittoria.Inventario.Presentation.Mapper;

internal static class KardexMapper
{
    public static KardexEntradaResponseDto ToResponse(this KardexEntrada origen)
        => new()
        {
            IdKardexEntrada = origen.IdKardexEntrada,
            IdEspecialidad = origen.IdEspecialidad,
            Especialidad = origen.Especialidad,
            IdMaterial = origen.IdMaterial,
            CodigoMaterial = origen.CodigoMaterial,
            Nombre = origen.Nombre,
            IdProveedor = origen.IdProveedor,
            Proveedor = origen.Proveedor,
            IdProyecto = origen.IdProyecto,
            Proyecto = origen.Proyecto,
            NumeroDocumento = origen.NumeroDocumento,
            Fecha = origen.Fecha,
            Cantidad = origen.Cantidad,
            Observacion = origen.Observacion,
            FechaCreacion = origen.FechaCreacion
        };

    public static ItemKardexSalida ToModel(this KardexSalidaItemCreateDto origen)
        => new()
        {
            IdMaterial = origen.IdMaterial,
            Cantidad = origen.Cantidad,
            Observacion = origen.Observacion
        };

    public static KardexSalidaResponseDto ToResponse(this KardexSalida origen)
        => new()
        {
            IdKardexSalida = origen.IdKardexSalida,
            IdEspecialidad = origen.IdEspecialidad,
            Especialidad = origen.Especialidad,
            IdProyecto = origen.IdProyecto,
            Proyecto = origen.Proyecto,
            NumeroDocumento = origen.NumeroDocumento,
            Fecha = origen.Fecha,
            Solicitante = origen.Solicitante,
            Observacion = origen.Observacion,
            IdKardexSalidaDetalle = origen.IdKardexSalidaDetalle,
            IdMaterial = origen.IdMaterial,
            CodigoMaterial = origen.CodigoMaterial,
            Nombre = origen.Nombre,
            Cantidad = origen.Cantidad,
            DetalleObservacion = origen.DetalleObservacion,
            FechaCreacion = origen.FechaCreacion
        };

    public static RegistroKardexEntrada ToModel(this KardexEntradaCreateDto origen)
        => new()
        {
            IdKardexEntrada = origen.IdKardexEntrada,
            IdMaterial = origen.IdMaterial,
            IdProveedor = origen.IdProveedor,
            IdProyecto = origen.IdProyecto,
            NumeroDocumento = origen.NumeroDocumento,
            Fecha = origen.Fecha,
            Cantidad = origen.Cantidad,
            Observacion = origen.Observacion
        };

    public static RegistroKardexSalida ToModel(this KardexSalidaCreateDto origen)
        => new()
        {
            IdKardexSalida = origen.IdKardexSalida,
            IdProyecto = origen.IdProyecto,
            NumeroDocumento = origen.NumeroDocumento,
            Fecha = origen.Fecha,
            Solicitante = origen.Solicitante,
            Observacion = origen.Observacion,
            Items = origen.Items?.Select(item => item is null ? null! : item.ToModel()).ToList()!
        };

    public static KardexStockActualResponseDto ToResponse(this StockKardex origen)
        => new()
        {
            IdKardexStock = origen.IdKardexStock,
            IdMaterial = origen.IdMaterial,
            CodigoMaterial = origen.CodigoMaterial,
            Nombre = origen.Nombre,
            UnidadMedida = origen.UnidadMedida,
            IdEspecialidad = origen.IdEspecialidad,
            Especialidad = origen.Especialidad,
            TotalEntrada = origen.TotalEntrada,
            TotalSalida = origen.TotalSalida,
            Stock = origen.Stock,
            FechaUltimaMovimiento = origen.FechaUltimaMovimiento
        };

}
