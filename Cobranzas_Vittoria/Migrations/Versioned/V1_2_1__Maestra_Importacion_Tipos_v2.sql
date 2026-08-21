-- =============================================================================
-- Migracion versionada: V1_2_1__Maestra_Importacion_Tipos_v2.sql
--
-- Refactor de importacion masiva del modulo Material (Fase 2 del diseno
-- v2 del feature). Cambios respecto a V1_1_2:
--
--   1. Reemplaza el TVP de Material por una version _v2 con un shape distinto:
--      los IDs de las entidades catalogo (Especialidad, UnidadMedida) se
--      reciben ya resueltos, no se descubren en el SP. Esto permite atomicidad
--      real: las altas de catalogos y los INSERT de Material viven en la
--      MISMA transaccion que abre el processor.
--
--   2. Agrega indices UNIQUE filtrados (case-insensitive + accent-insensitive
--      para los acentos castellanos mas comunes) sobre
--      maestra.Especialidad.Nombre y maestra.UnidadMedida.Nombre. La unicidad
--      se implementa con una columna computada PERSISTED (NombreNormalizado)
--      sobre la que se crea el indice. SQL Server exige que las columnas de
--      indices UNIQUE sean PERSISTED si son expresiones.
--
--   3. El TVP viejo (TVP_Material) y el SP viejo (usp_Material_CargaMasiva)
--      se conservaron con sus nombres historicos en esta migracion para
--      mantener la convivenvia con v1. Posteriormente, en V1_2_3, ambos
--      fueron dropeados al confirmarse que v1 era codigo muerto. La v2
--      del SP usa TVP_Material_v2.
--
-- Convenciones:
--   - Nombre: TVP_<Entidad>_v2 (sufijo _v2 para evitar colision con V1_1_2).
--   - Columnas: IdEspecialidad (FK resuelta), Codigo (requerido, sin
--               autogenerar), Descripcion, IdUnidadMedida (FK resuelta, NULLable
--               para preservar compatibilidad con registros que solo tienen
--               texto), UnidadMedida (texto libre del archivo), _Fila.
--   - nvarchar para texto: la pila ADO.NET es Unicode por defecto.
--
-- NOTA sobre normalizacion: la base usa SQL_Latin1_General_CP1_CI_AS (CI pero
-- no AI), que es INCOMPATIBLE con Latin1_General_CI_AI en columnas
-- computadas PERSISTED. Por eso esta migracion normaliza con REPLAZOS
-- EXPLICITOS de los caracteres acentuados castellanos mas comunes. Esto
-- coincide con la normalizacion del cliente en
-- ResolvedorEntidadesService.Normalizar (NFD + remove diacritics) para los
-- caracteres mas usados. La comparacion sigue siendo case-insensitive
-- gracias al collation de la columna base.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- Drop defensivo del TVP nuevo: si por algun motivo quedo creado a medias en
-- una corrida fallida, lo limpiamos antes de CREATE TYPE.
-- -----------------------------------------------------------------------------
IF TYPE_ID('maestra.TVP_Material_v2') IS NOT NULL
    DROP TYPE maestra.TVP_Material_v2;
GO


-- -----------------------------------------------------------------------------
-- Material v2: shape con IDs ya resueltos.
--   - IdEspecialidad: FK resuelta por el processor (creada en transaccion si
--                     el nombre no existe).
--   - Codigo:         REQUERIDO, sin autogeneracion. Lo trae el usuario.
--   - Descripcion:    REQUERIDO, viene de la columna "Nombre" del archivo.
--   - IdUnidadMedida: FK resuelta por el processor. NULL si la fila viene
--                     con UnidadMedida vacia.
--   - UnidadMedida:   REQUERIDO, texto libre. Se persiste tal cual en la
--                     columna maestra.Material.UnidadMedida (max 30 chars).
--   - _Fila:          metadata para reportar errores con contexto.
-- -----------------------------------------------------------------------------
CREATE TYPE maestra.TVP_Material_v2 AS TABLE (
    IdEspecialidad    INT            NOT NULL,
    Codigo            NVARCHAR(50)   NOT NULL,
    Descripcion       NVARCHAR(200)  NOT NULL,
    IdUnidadMedida    INT            NULL,
    UnidadMedida      NVARCHAR(30)   NOT NULL,
    _Fila             INT            NOT NULL
);
GO


-- -----------------------------------------------------------------------------
-- Columna computada PERSISTED + indice UNIQUE filtrado para Especialidad.Nombre.
--
-- Por que PERSISTED: SQL Server NO permite expresiones/funciones dentro de
-- columnas de un UNIQUE INDEX a menos que la columna sea PERSISTED. La columna
-- computada NombreNormalizado se materializa en disco y se mantiene en sync
-- automaticamente con el valor de Nombre.
--
-- Por que REPLACE en vez de COLLATE Latin1_General_CI_AI: la base tiene
-- collation SQL_Latin1_General_CP1_CI_AS, que es INCOMPATIBLE con
-- Latin1_General_CI_AI (tienen code pages distintas). SQL Server rechaza
-- mezclar collations incompatibles en columnas computadas PERSISTED. Por eso
-- normalizamos explicitamente los caracteres acentuados castellanos mas
-- comunes. La comparacion final sigue siendo CI (case-insensitive) gracias
-- al collation de la columna base.
--
-- Por que WHERE Activo = 1: solo Especialidades activas participan en la
-- unicidad. Las inactivas pueden tener el mismo nombre (escenario raro
-- pero posible: reactivar una especialidad con un nombre que se uso antes).
-- -----------------------------------------------------------------------------

IF COL_LENGTH('maestra.Especialidad', 'NombreNormalizado') IS NULL
BEGIN
    ALTER TABLE maestra.Especialidad
        ADD NombreNormalizado AS (
            UPPER(LTRIM(RTRIM(
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    Nombre,
                    N'á', N'A'), N'é', N'E'), N'í', N'I'), N'ó', N'O'), N'ú', N'U'),
                    N'Á', N'A'), N'É', N'E'), N'Í', N'I'), N'Ó', N'O'), N'Ú', N'U'),
                    N'ñ', N'N'), N'Ñ', N'N')
            )))
        ) PERSISTED;
END
GO

-- -----------------------------------------------------------------------------
-- Limpieza de duplicados pre-existentes: si los datos seed o cargas anteriores
-- dejaron varias Especialidades activas con el mismo Nombre (case-insensitive
-- + accent-insensitive), el indice UNIQUE filtrado por Activo=1 no podra
-- crearse. Desactivamos (Activo = 0) los duplicados, conservando el de menor
-- Id por NombreNormalizado.
-- -----------------------------------------------------------------------------
UPDATE e
SET e.Activo = 0,
    e.Descripcion = ISNULL(e.Descripcion, '') + ' [Desactivada por migracion v1.2.1: duplicado de nombre normalizado]'
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

-- Si el indice ya existe (migracion aplicada parcialmente antes), lo eliminamos
-- para que el bloque de CREATE INDEX de abajo sea idempotente.
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
-- Idem para UnidadMedida.Nombre.
-- -----------------------------------------------------------------------------

IF COL_LENGTH('maestra.UnidadMedida', 'NombreNormalizado') IS NULL
BEGIN
    ALTER TABLE maestra.UnidadMedida
        ADD NombreNormalizado AS (
            UPPER(LTRIM(RTRIM(
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    Nombre,
                    N'á', N'A'), N'é', N'E'), N'í', N'I'), N'ó', N'O'), N'ú', N'U'),
                    N'Á', N'A'), N'É', N'E'), N'Í', N'I'), N'Ó', N'O'), N'Ú', N'U'),
                    N'ñ', N'N'), N'Ñ', N'N')
            )))
        ) PERSISTED;
END
GO

-- Limpieza de duplicados pre-existentes en UnidadMedida.
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
