CREATE OR ALTER VIEW contable.vw_CotizacionMaterialesPorProyecto
AS
SELECT
    p.IdProyecto,
    p.NombreProyecto AS Proyecto,
    SUM(ISNULL(c.Cotizacion, 0)) AS CotizacionMateriales
FROM
    maestra.Proyecto p
LEFT JOIN contable.CotizacionMaterialEspecialidad c
    ON
    c.IdProyecto = p.IdProyecto
    AND ISNULL(c.Activo, 1) = 1
WHERE
    ISNULL(p.Activo, 1) = 1
GROUP BY
    p.IdProyecto,
    p.NombreProyecto;

CREATE OR ALTER VIEW contable.vw_CotizacionMaterialesResumenTodosProyectos
AS
WITH Cotizaciones AS
(
SELECT
        c.IdProyecto,
        p.NombreProyecto AS Proyecto,
        c.IdEspecialidad,
        e.Nombre AS Especialidad,
        UPPER(LTRIM(RTRIM(e.Nombre))) COLLATE Latin1_General_CI_AI AS EspecialidadKey,
        SUM(ISNULL(c.Cotizacion, 0)) AS Cotizacion
FROM
    contable.CotizacionMaterialEspecialidad c
INNER JOIN maestra.Proyecto p
        ON
    p.IdProyecto = c.IdProyecto
INNER JOIN maestra.Especialidad e
        ON
    e.IdEspecialidad = c.IdEspecialidad
WHERE
    ISNULL(c.Activo, 1) = 1
        AND ISNULL(p.Activo, 1) = 1
            AND ISNULL(e.Activo, 1) = 1
        GROUP BY
            c.IdProyecto,
            p.NombreProyecto,
            c.IdEspecialidad,
            e.Nombre
),
Facturado AS
(
SELECT
        COALESCE(oc.IdProyecto, r.IdProyecto) AS IdProyecto,
        p.NombreProyecto AS Proyecto,
        m.IdEspecialidad,
        e.Nombre AS Especialidad,
        UPPER(LTRIM(RTRIM(e.Nombre))) COLLATE Latin1_General_CI_AI AS EspecialidadKey,
        SUM(
            ISNULL(
                TRY_CONVERT(DECIMAL(18, 2), d.Subtotal),
                ISNULL(TRY_CONVERT(DECIMAL(18, 2), d.Cantidad), 0) * ISNULL(TRY_CONVERT(DECIMAL(18, 2), d.PrecioUnitario), 0)
            )
        ) AS Facturado
FROM
    compras.Compra c
INNER JOIN compras.CompraDetalle d
        ON
    d.IdCompra = c.IdCompra
INNER JOIN maestra.Material m
        ON
    m.IdMaterial = d.IdMaterial
INNER JOIN maestra.Especialidad e
        ON
    e.IdEspecialidad = m.IdEspecialidad
INNER JOIN compras.OrdenCompra oc
        ON
    oc.IdOrdenCompra = c.IdOrdenCompra
LEFT JOIN compras.Requerimiento r
        ON
    r.IdRequerimiento = oc.IdRequerimiento
INNER JOIN maestra.Proyecto p
        ON
    p.IdProyecto = COALESCE(oc.IdProyecto, r.IdProyecto)
WHERE
    COALESCE(oc.IdProyecto, r.IdProyecto) IS NOT NULL
GROUP BY
    COALESCE(oc.IdProyecto, r.IdProyecto),
    p.NombreProyecto,
    m.IdEspecialidad,
    e.Nombre
),
FacturadoConCotizacion AS
(
SELECT
        f.IdProyecto,
        f.Proyecto,
        f.IdEspecialidad,
        f.Especialidad,
        ISNULL(ca.CotizacionAplicable, 0) AS Cotizacion,
        ISNULL(f.Facturado, 0) AS Facturado
FROM
    Facturado f
    OUTER APPLY
    (
    SELECT
        SUM(c.Cotizacion) AS CotizacionAplicable
    FROM
        Cotizaciones c
    WHERE
        c.IdProyecto = f.IdProyecto
        AND
          (
                c.IdEspecialidad = f.IdEspecialidad
            OR c.EspecialidadKey = f.EspecialidadKey
            OR EXISTS
                (
            SELECT
                1
            FROM
                STRING_SPLIT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(f.Especialidad, ';', ','), '/', ','), '|', ','), '+', ','), '&', ','), ',') s
            WHERE
                UPPER(LTRIM(RTRIM(s.value))) COLLATE Latin1_General_CI_AI = c.EspecialidadKey
                )
          )
    ) ca
),
CotizacionesSinFacturado AS
(
SELECT
        c.IdProyecto,
        c.Proyecto,
        c.IdEspecialidad,
        c.Especialidad,
        c.Cotizacion,
        CAST(0 AS DECIMAL(18, 2)) AS Facturado
FROM
    Cotizaciones c
WHERE
    ISNULL(c.Cotizacion, 0) <> 0
        AND NOT EXISTS
      (
        SELECT
            1
        FROM
            Facturado f
        WHERE
            f.IdProyecto = c.IdProyecto
            AND
            (
                f.IdEspecialidad = c.IdEspecialidad
                OR f.EspecialidadKey = c.EspecialidadKey
                OR EXISTS
                (
                SELECT
                    1
                FROM
                    STRING_SPLIT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(f.Especialidad, ';', ','), '/', ','), '|', ','), '+', ','), '&', ','), ',') s
                WHERE
                    UPPER(LTRIM(RTRIM(s.value))) COLLATE Latin1_General_CI_AI = c.EspecialidadKey
                )
            )
      )
)
SELECT
    IdProyecto,
    Proyecto,
    IdEspecialidad,
    Especialidad,
    Cotizacion,
    Facturado,
    Cotizacion - Facturado AS Saldo
FROM
    FacturadoConCotizacion
UNION ALL
SELECT
    IdProyecto,
    Proyecto,
    IdEspecialidad,
    Especialidad,
    Cotizacion,
    Facturado,
    Cotizacion - Facturado AS Saldo
FROM
    CotizacionesSinFacturado;

CREATE OR ALTER VIEW [contable].[vw_PresupuestoProyectoResumen]
AS
WITH Presupuesto AS
(
SELECT
        pp.IdPresupuestoProyecto,
        pp.IdProyecto,
        p.NombreProyecto,
        CAST(ISNULL(SUM(CASE WHEN ISNULL(ppd.Activo, 1) = 1 THEN ISNULL(ppd.Soles, 0) ELSE 0 END), 0) AS DECIMAL(18, 2)) AS TotalPresupuesto
FROM
    [contable].[PresupuestoProyecto] pp
INNER JOIN [maestra].[Proyecto] p
        ON
    p.IdProyecto = pp.IdProyecto
LEFT JOIN [contable].[PresupuestoProyectoDetalle] ppd
        ON
    ppd.IdPresupuestoProyecto = pp.IdPresupuestoProyecto
WHERE
    ISNULL(pp.Activo, 1) = 1
GROUP BY
        pp.IdPresupuestoProyecto,
        pp.IdProyecto,
        p.NombreProyecto
),
ComprasProyecto AS
(
SELECT
        oc.IdProyecto,
        CAST(ISNULL(SUM(ISNULL(c.MontoTotal, 0)), 0) AS DECIMAL(18, 2)) AS TotalCompras
FROM
    [compras].[OrdenCompra] oc
LEFT JOIN [compras].[Compra] c
        ON
    c.IdOrdenCompra = oc.IdOrdenCompra
GROUP BY
        oc.IdProyecto
)
SELECT
    pr.IdPresupuestoProyecto,
    pr.IdProyecto,
    pr.NombreProyecto,
    pr.TotalPresupuesto,
    CAST(ISNULL(cp.TotalCompras, 0) AS DECIMAL(18, 2)) AS TotalCompras,
    CAST(pr.TotalPresupuesto - ISNULL(cp.TotalCompras, 0) AS DECIMAL(18, 2)) AS SaldoRestante
FROM
    Presupuesto pr
LEFT JOIN ComprasProyecto cp
    ON
    cp.IdProyecto = pr.IdProyecto;

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
