CREATE OR ALTER VIEW [almacen].[vw_Kardex_DesdeComprasYMovimientos]
AS
WITH BaseCompras AS
(
SELECT
        km.IdKardexMovimiento,
        km.IdMaterial,
        km.IdEspecialidad,
        km.TipoMovimiento,
        km.FechaMovimiento,
        km.CantidadEntrada,
        km.CantidadSalida,
        km.StockResultante,
        km.IdCompra,
        km.IdOrdenCompra,
        km.Observacion,
        km.FechaIngresoAlmacen,
        km.FechaSalidaAlmacen,
        km.FechaCreacion
FROM
    almacen.KardexMovimiento km
),
Enriquecido AS
(
SELECT
        b.IdKardexMovimiento,
        b.IdMaterial,
        m.Descripcion AS Material,
        COALESCE(b.IdEspecialidad, m.IdEspecialidad) AS IdEspecialidad,
        e.Nombre AS Especialidad,
        b.TipoMovimiento,
        b.FechaMovimiento,
        CAST(ISNULL(b.CantidadEntrada, 0) AS DECIMAL(18, 2)) AS Entrada,
        CAST(ISNULL(b.CantidadSalida, 0) AS DECIMAL(18, 2)) AS Salida,
        CAST(ISNULL(b.StockResultante, 0) AS DECIMAL(18, 2)) AS Stock,
        b.IdCompra,
        c.NumeroCompra,
        b.IdOrdenCompra,
        oc.NumeroOrdenCompra,
        ISNULL(b.Observacion, '') AS Observacion,
        b.FechaIngresoAlmacen,
        b.FechaSalidaAlmacen,
        b.FechaCreacion
FROM
    BaseCompras b
LEFT JOIN maestra.Material m ON
    m.IdMaterial = b.IdMaterial
LEFT JOIN maestra.Especialidad e ON
    e.IdEspecialidad = COALESCE(b.IdEspecialidad, m.IdEspecialidad)
LEFT JOIN compras.Compra c ON
    c.IdCompra = b.IdCompra
LEFT JOIN compras.OrdenCompra oc ON
    oc.IdOrdenCompra = b.IdOrdenCompra
)
SELECT
    *
FROM
    Enriquecido;
GO

CREATE OR ALTER VIEW [almacen].[vw_Kardex_PorEspecialidad]
AS
SELECT
    km.IdKardexMovimiento,
    km.IdEspecialidad,
    e.Nombre AS Especialidad,
    km.IdMaterial,
    m.Descripcion AS Material,
    m.UnidadMedida,
    km.TipoMovimiento,
    km.FechaMovimiento,
    km.CantidadEntrada,
    km.CantidadSalida,
    km.StockResultante,
    km.IdCompra,
    km.IdOrdenCompra,
    km.Observacion,
    km.FechaIngresoAlmacen,
    km.FechaSalidaAlmacen,
    km.FechaCreacion
FROM
    almacen.KardexMovimiento km
INNER JOIN maestra.Material m
    ON
    m.IdMaterial = km.IdMaterial
LEFT JOIN maestra.Especialidad e
    ON
    e.IdEspecialidad = km.IdEspecialidad;
GO

