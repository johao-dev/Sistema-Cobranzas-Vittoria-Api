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


-- =============================================================================
-- Modulo Inventario (Kardex manual entradas/salidas/stock)
-- Version 1.2.0. Vista 100% nueva. Se agrega a este archivo sin modificar
-- ninguna de las vistas existentes arriba (vw_Kardex_DesdeComprasYMovimientos,
-- vw_Kardex_PorEspecialidad), que siguen ligadas al kardex de Compras
-- (KardexMovimiento).
--
-- Esta vista expone el inventario consolidado en tiempo real mantenido por
-- los SPs del modulo Inventario (almacen.KardexStock). Es un wrapper
-- autocontenido pensado para:
--   - Reportes ad-hoc y consultas desde SSMS.
--   - Consumidores externos que prefieran SELECT directo a la tabla.
--   - Tests de integracion que verifiquen el estado consolidado.
--
-- IMPORTANTE: usp_Kardex_StockActual_Listar (R__Almacen_SPs.sql) NO
-- depende de esta vista. Inlina los mismos JOINs para mantenerse
-- autocontenido y orden-independiente.
-- Si se necesita una unica fuente de verdad, considerar migrar el SP a
-- "SELECT ... FROM almacen.vw_Kardex_StockActual_v2" en una fase futura.
-- =============================================================================


CREATE OR ALTER VIEW [almacen].[vw_Kardex_StockActual_v2]
AS
SELECT
    ks.IdKardexStock,
    ks.IdMaterial,
    m.Codigo          AS CodigoMaterial,
    m.Descripcion     AS Nombre,
    m.UnidadMedida,
    ks.IdEspecialidad,
    e.Nombre          AS Especialidad,
    ks.IdProyecto,
    pr.NombreProyecto AS Proyecto,
    ks.TotalEntrada,
    ks.TotalSalida,
    ks.Stock,
    ks.FechaUltimaMovimiento
FROM almacen.KardexStock ks
INNER JOIN maestra.Material      m  ON m.IdMaterial     = ks.IdMaterial
INNER JOIN maestra.Especialidad  e  ON e.IdEspecialidad = ks.IdEspecialidad
LEFT  JOIN maestra.Proyecto      pr ON pr.IdProyecto    = ks.IdProyecto;
