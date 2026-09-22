/*
  Retira los dos modelos sustituidos. Los datos se archivan como JSON antes del
  DROP para que la decisión no destruya evidencia histórica ni intente inferir
  una equivalencia inexistente con ControlPresupuestario.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'contable.LegacyRetiroSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE contable.LegacyRetiroSnapshot
    (
        Objeto SYSNAME NOT NULL,
        IdLegacy INT NOT NULL,
        DatosJson NVARCHAR(MAX) NOT NULL,
        FechaArchivo DATETIME2(0) NOT NULL
            CONSTRAINT DF_LegacyRetiroSnapshot_FechaArchivo DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_LegacyRetiroSnapshot PRIMARY KEY (Objeto, IdLegacy),
        CONSTRAINT CK_LegacyRetiroSnapshot_Json CHECK (ISJSON(DatosJson) = 1)
    );
END;

DROP PROCEDURE IF EXISTS contable.usp_CotizacionMaterialesResumen_Listar;
DROP PROCEDURE IF EXISTS contable.usp_CotizacionMaterialesTotalPorProyecto_Listar;
DROP PROCEDURE IF EXISTS contable.usp_PresupuestoProyecto_Delete;
DROP PROCEDURE IF EXISTS contable.usp_PresupuestoProyecto_Get;
DROP PROCEDURE IF EXISTS contable.usp_PresupuestoProyecto_List;
DROP PROCEDURE IF EXISTS contable.usp_PresupuestoProyecto_Upsert;
DROP VIEW IF EXISTS contable.vw_CotizacionMaterialesPorProyecto;
DROP VIEW IF EXISTS contable.vw_CotizacionMaterialesResumenTodosProyectos;
DROP VIEW IF EXISTS contable.vw_PresupuestoProyectoResumen;

IF EXISTS
(
    SELECT 1 FROM sys.sql_expression_dependencies d
    LEFT JOIN sys.objects o ON o.object_id = d.referencing_id
    WHERE d.referenced_id IN
    (
        OBJECT_ID(N'contable.CotizacionMaterialEspecialidad'),
        OBJECT_ID(N'contable.PresupuestoProyecto'),
        OBJECT_ID(N'contable.PresupuestoProyectoDetalle')
    )
      AND ISNULL(o.parent_object_id, 0) NOT IN
      (
        OBJECT_ID(N'contable.CotizacionMaterialEspecialidad'),
        OBJECT_ID(N'contable.PresupuestoProyecto'),
        OBJECT_ID(N'contable.PresupuestoProyectoDetalle')
      )
      AND d.referencing_id NOT IN
      (
        OBJECT_ID(N'contable.CotizacionMaterialEspecialidad'),
        OBJECT_ID(N'contable.PresupuestoProyecto'),
        OBJECT_ID(N'contable.PresupuestoProyectoDetalle')
      )
)
    THROW 51560, 'DEPENDENCIA_LEGACY_INESPERADA: existe un objeto SQL que aún referencia presupuesto/cotización contable legacy.', 1;

IF EXISTS
(
    SELECT 1
    FROM sys.foreign_keys fk
    WHERE fk.referenced_object_id IN
    (
        OBJECT_ID(N'contable.CotizacionMaterialEspecialidad'),
        OBJECT_ID(N'contable.PresupuestoProyecto'),
        OBJECT_ID(N'contable.PresupuestoProyectoDetalle')
    )
      AND fk.parent_object_id <> OBJECT_ID(N'contable.PresupuestoProyectoDetalle')
)
    THROW 51561, 'DEPENDENCIA_LEGACY_INESPERADA: existe una FK externa hacia presupuesto/cotización contable legacy.', 1;

INSERT INTO contable.LegacyRetiroSnapshot (Objeto, IdLegacy, DatosJson)
SELECT N'contable.CotizacionMaterialEspecialidad', c.IdCotizacionMaterialEspecialidad,
       (SELECT c.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
FROM contable.CotizacionMaterialEspecialidad c;

INSERT INTO contable.LegacyRetiroSnapshot (Objeto, IdLegacy, DatosJson)
SELECT N'contable.PresupuestoProyecto', p.IdPresupuestoProyecto,
       (SELECT p.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
FROM contable.PresupuestoProyecto p;

INSERT INTO contable.LegacyRetiroSnapshot (Objeto, IdLegacy, DatosJson)
SELECT N'contable.PresupuestoProyectoDetalle', d.IdPresupuestoProyectoDetalle,
       (SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
FROM contable.PresupuestoProyectoDetalle d;

DROP TABLE contable.CotizacionMaterialEspecialidad;
DROP TABLE contable.PresupuestoProyectoDetalle;
DROP TABLE contable.PresupuestoProyecto;

IF TYPE_ID(N'contable.TVP_PresupuestoProyectoDetalle') IS NOT NULL
    DROP TYPE contable.TVP_PresupuestoProyectoDetalle;

COMMIT TRANSACTION;
