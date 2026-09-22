CREATE OR ALTER VIEW [contable].[vw_ValorizacionDetalleCalculado]
AS
WITH Base AS
(
SELECT
        v.IdValorizacion,
        v.NumeroValorizacion,
        v.IdProyecto,
        p.NombreProyecto,
        v.IdProveedor,
        pr.RazonSocial AS Proveedor,
        v.IdEspecialidad,
        e.Nombre AS Especialidad,
        v.Empresa,
        v.Servicio,
        v.Moneda,
        v.Cotizacion,
        v.PorcentajeGarantia AS PorcentajeGarantiaCabecera,
        v.PorcentajeDetraccion AS PorcentajeDetraccionCabecera,
        d.IdValorizacionDetalle,
        d.FechaFactura,
        d.NumeroFactura,
        d.BaseImponible,
        d.Igv,
        d.MontoFactura,
        d.Descripcion,
        d.MontoDetraccion,
        d.MontoGarantia,
        d.OtrosDescuentos,
        d.MontoAbonar,
        d.FechaTransferencia,
        d.NumeroOperacion,
        d.BancoTransferencia,
        d.BancoDestino,
        d.MontoTransferido,
        d.MontoAFavor,
        d.MontoDeuda,
        d.Activo
FROM
    contable.Valorizacion v
INNER JOIN contable.ValorizacionDetalle d
        ON
    d.IdValorizacion = v.IdValorizacion
INNER JOIN maestra.Proveedor pr
        ON
    pr.IdProveedor = v.IdProveedor
INNER JOIN maestra.Especialidad e
        ON
    e.IdEspecialidad = v.IdEspecialidad
LEFT JOIN maestra.Proyecto p
        ON
    p.IdProyecto = v.IdProyecto
WHERE
    v.Activo = 1
    AND d.Activo = 1
)
SELECT
    b.*,
    CAST(ROUND(CASE WHEN b.Cotizacion > 0 THEN b.MontoFactura / b.Cotizacion ELSE 0 END, 6) AS DECIMAL(18, 6)) AS PorcentajeAvance,
    CAST(ROUND(
        SUM(CASE WHEN b.Cotizacion > 0 THEN b.MontoFactura / b.Cotizacion ELSE 0 END)
        OVER (PARTITION BY b.IdValorizacion ORDER BY ISNULL(b.FechaFactura, '19000101'), b.IdValorizacionDetalle ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
    , 6) AS DECIMAL(18, 6)) AS PorcentajeAcumulado,
    CAST(ROUND(
        ISNULL(
            SUM(CASE WHEN b.Cotizacion > 0 THEN b.MontoFactura / b.Cotizacion ELSE 0 END)
            OVER (PARTITION BY b.IdValorizacion ORDER BY ISNULL(b.FechaFactura, '19000101'), b.IdValorizacionDetalle ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
        , 0)
    , 6) AS DECIMAL(18, 6)) AS PorcentajeInicial,
    CAST(ROUND(
        ISNULL(
            SUM(CASE WHEN b.Cotizacion > 0 THEN b.MontoFactura / b.Cotizacion ELSE 0 END)
            OVER (PARTITION BY b.IdValorizacion ORDER BY ISNULL(b.FechaFactura, '19000101'), b.IdValorizacionDetalle ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
        , 0)
        + CASE WHEN b.Cotizacion > 0 THEN b.MontoFactura / b.Cotizacion ELSE 0 END
    , 6) AS DECIMAL(18, 6)) AS PorcentajeFinal
FROM
    Base b;
GO

CREATE OR ALTER VIEW [contable].[vw_ValorizacionResumen]
AS
SELECT
    v.IdValorizacion,
    v.NumeroValorizacion,
    v.IdProyecto,
    p.NombreProyecto,
    v.IdProveedor,
    pr.RazonSocial AS Proveedor,
    v.IdEspecialidad,
    e.Nombre AS Especialidad,
    v.Empresa,
    v.Servicio,
    v.Moneda,
    v.Cotizacion,
    v.PorcentajeGarantia,
    v.PorcentajeDetraccion,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoFactura END), 0) AS DECIMAL(18, 2)) AS Facturado,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoTransferido END), 0) AS DECIMAL(18, 2)) AS Transferido,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoGarantia END), 0) AS DECIMAL(18, 2)) AS GarantiaRetenida,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoDetraccion END), 0) AS DECIMAL(18, 2)) AS DetraccionAcumulada,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.OtrosDescuentos END), 0) AS DECIMAL(18, 2)) AS OtrosDescuentos,
    CAST(v.Cotizacion - ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoFactura END), 0) AS DECIMAL(18, 2)) AS Resta,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoGarantia END), 0) + (v.Cotizacion - ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoFactura END), 0)) AS DECIMAL(18, 2)) AS Liquidar,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoAFavor END), 0) AS DECIMAL(18, 2)) AS AFavor,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoDeuda END), 0) AS DECIMAL(18, 2)) AS Deuda,
    v.Activo,
    v.FechaCreacion
FROM
    contable.Valorizacion v
INNER JOIN maestra.Proveedor pr
    ON
    pr.IdProveedor = v.IdProveedor
INNER JOIN maestra.Especialidad e
    ON
    e.IdEspecialidad = v.IdEspecialidad
LEFT JOIN maestra.Proyecto p
    ON
    p.IdProyecto = v.IdProyecto
LEFT JOIN contable.ValorizacionDetalle d
    ON
    d.IdValorizacion = v.IdValorizacion
WHERE
    v.Activo = 1
GROUP BY
    v.IdValorizacion,
    v.NumeroValorizacion,
    v.IdProyecto,
    p.NombreProyecto,
    v.IdProveedor,
    pr.RazonSocial,
    v.IdEspecialidad,
    e.Nombre,
    v.Empresa,
    v.Servicio,
    v.Moneda,
    v.Cotizacion,
    v.PorcentajeGarantia,
    v.PorcentajeDetraccion,
    v.Activo,
    v.FechaCreacion;
