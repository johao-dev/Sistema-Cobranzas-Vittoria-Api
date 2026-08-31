-- =============================================================================
-- Migracion versionada: V1_2_1__Almacen_Stock_Global.sql
--
-- Cambia el modelo de KardexStock de "stock por triada"
-- (IdMaterial, IdEspecialidad, IdProyecto) a "stock global"
-- (IdMaterial, IdEspecialidad). El proyecto sigue existiendo como
-- etiqueta en KardexEntrada/KardexSalida, pero ya no segmenta el
-- inventario consolidado.
--
-- Esta migracion asume que V1_2_0__Almacen_EntradasSalidas_DDL.sql
-- ya fue aplicada en el ambiente.
--
-- Objetos modificados:
--   - almacen.KardexStock         : elimina IdProyecto de la clave natural.
--   - almacen.vw_Kardex_StockActual_v2 : se elimina (no tenia consumidores).
--
-- Regla de negocio resultante:
--   El stock es GLOBAL por material + especialidad. Cualquier entrada o
--   salida, con o sin proyecto, afecta la misma fila de KardexStock.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Paso 1: Eliminar la vista que ya no se usa y que depende de la estructura
-- actual de KardexStock.
-- -----------------------------------------------------------------------------
IF OBJECT_ID('almacen.vw_Kardex_StockActual_v2', 'V') IS NOT NULL
    DROP VIEW almacen.vw_Kardex_StockActual_v2;
GO

-- -----------------------------------------------------------------------------
-- Paso 2: Consolidar filas existentes de KardexStock.
--   Si un mismo (IdMaterial, IdEspecialidad) tiene stock registrado en
--   multiples proyectos (incluyendo NULL), se suman y se deja una sola fila
--   global con IdProyecto = NULL.
-- -----------------------------------------------------------------------------
ALTER TABLE almacen.KardexStock DROP CONSTRAINT FK_KardexStock_Proyecto;
GO

-- Guardar el consolidado en una tabla temporal antes de borrar el origen.
-- Se recalcula Stock = TotalEntrada - TotalSalida para garantizar el invariante.
SELECT
    IdMaterial,
    IdEspecialidad,
    SUM(TotalEntrada)                   AS TotalEntrada,
    SUM(TotalSalida)                    AS TotalSalida,
    SUM(TotalEntrada) - SUM(TotalSalida) AS Stock,
    MAX(FechaUltimaMovimiento)          AS FechaUltimaMovimiento
INTO #KardexStockConsolidado
FROM almacen.KardexStock
GROUP BY IdMaterial, IdEspecialidad;
GO

DELETE FROM almacen.KardexStock;
GO

INSERT INTO almacen.KardexStock (
    IdMaterial,
    IdEspecialidad,
    IdProyecto,
    TotalEntrada,
    TotalSalida,
    Stock,
    FechaUltimaMovimiento
)
SELECT
    IdMaterial,
    IdEspecialidad,
    NULL,
    TotalEntrada,
    TotalSalida,
    Stock,
    FechaUltimaMovimiento
FROM #KardexStockConsolidado;
GO

DROP TABLE #KardexStockConsolidado;
GO

-- -----------------------------------------------------------------------------
-- Paso 3: Cambiar la unique constraint de triada a (Material, Especialidad).
-- -----------------------------------------------------------------------------
ALTER TABLE almacen.KardexStock DROP CONSTRAINT UQ_KardexStock_Material_Especialidad_Proyecto;
GO

ALTER TABLE almacen.KardexStock
    ADD CONSTRAINT UQ_KardexStock_Material_Especialidad
    UNIQUE (IdMaterial, IdEspecialidad);
GO

-- -----------------------------------------------------------------------------
-- Paso 4: Eliminar la columna IdProyecto de KardexStock. El proyecto sigue
-- existiendo en KardexEntrada y KardexSalida como etiqueta informativa.
-- -----------------------------------------------------------------------------
ALTER TABLE almacen.KardexStock DROP COLUMN IdProyecto;
GO
