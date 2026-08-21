-- =============================================================================
-- Migracion versionada: V1_2_2__Maestra_Importacion_Fix_v2.sql
--
-- Reparacion para el caso en que V1_2_1 (que agrega la columna computada
-- PERSISTED NombreNormalizado) se haya aplicado parcialmente: la columna fue
-- creada pero el indice UNIQUE fallo por duplicados pre-existentes en los
-- datos seed. Esta migracion:
--   1. Desactiva duplicados pre-existentes (Especialidad, UnidadMedida).
--   2. Crea los indices UNIQUE filtrados.
--
-- Es idempotente: si la columna computada o los indices ya existen, los
-- deja como estan; si no, los crea.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- Especialidad
-- -----------------------------------------------------------------------------
UPDATE e
SET e.Activo = 0,
    e.Descripcion = ISNULL(e.Descripcion, '') + ' [Desactivada por migracion v1.2.2: duplicado de nombre normalizado]'
FROM maestra.Especialidad e
INNER JOIN (
    SELECT NombreNormalizado, MIN(IdEspecialidad) AS IdConservar
    FROM maestra.Especialidad
    WHERE Activo = 1
    GROUP BY NombreNormalizado
    HAVING COUNT(*) > 1
) dups ON e.NombreNormalizado = dups.NombreNormalizado
      AND e.IdEspecialidad <> dups.IdConservar
      AND e.Activo = 1;
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Especialidad_NombreNormalizado_Activos'
      AND object_id = OBJECT_ID('maestra.Especialidad')
)
    DROP INDEX UX_Especialidad_NombreNormalizado_Activos ON maestra.Especialidad;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Especialidad_NombreNormalizado_Activos'
      AND object_id = OBJECT_ID('maestra.Especialidad')
)
BEGIN
    CREATE UNIQUE INDEX UX_Especialidad_NombreNormalizado_Activos
        ON maestra.Especialidad (NombreNormalizado)
        WHERE Activo = 1;
END
GO


-- -----------------------------------------------------------------------------
-- UnidadMedida
-- -----------------------------------------------------------------------------
UPDATE u
SET u.Activo = 0
FROM maestra.UnidadMedida u
INNER JOIN (
    SELECT NombreNormalizado, MIN(IdUnidadMedida) AS IdConservar
    FROM maestra.UnidadMedida
    WHERE Activo = 1
    GROUP BY NombreNormalizado
    HAVING COUNT(*) > 1
) dups ON u.NombreNormalizado = dups.NombreNormalizado
      AND u.IdUnidadMedida <> dups.IdConservar
      AND u.Activo = 1;
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_UnidadMedida_NombreNormalizado_Activos'
      AND object_id = OBJECT_ID('maestra.UnidadMedida')
)
    DROP INDEX UX_UnidadMedida_NombreNormalizado_Activos ON maestra.UnidadMedida;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_UnidadMedida_NombreNormalizado_Activos'
      AND object_id = OBJECT_ID('maestra.UnidadMedida')
)
BEGIN
    CREATE UNIQUE INDEX UX_UnidadMedida_NombreNormalizado_Activos
        ON maestra.UnidadMedida (NombreNormalizado)
        WHERE Activo = 1;
END
GO
