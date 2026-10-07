/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.7.0
Módulo      : Control Presupuestario / Gasto Directo
Descripción : Retira la clasificación de UI SeccionGasto del modelo económico.
===============================================================================

V2.6.3 ya ejecutó la migración histórica y pudo usar temporalmente las
secciones. Esta migración no elimina partidas ni gastos: las partidas HIST.*
quedan como partidas ordinarias y se retira únicamente la clasificación.

Los procedimientos dependientes se eliminan antes del cambio. DbUp ejecuta los
repeatables después de todas las migraciones versionadas y los recrea con el
contrato final.
===============================================================================
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Objetos operativos conocidos que pueden conservar dependencias compiladas.
DROP PROCEDURE IF EXISTS contable.usp_GastoDirecto_Listar;
DROP PROCEDURE IF EXISTS contable.usp_GastoDirecto_Obtener;
DROP PROCEDURE IF EXISTS contable.usp_GastoDirecto_Crear;
DROP PROCEDURE IF EXISTS contable.usp_GastoDirecto_Actualizar;
DROP PROCEDURE IF EXISTS contable.usp_GastoDirecto_CentrosCostoPorSeccion;
DROP PROCEDURE IF EXISTS contable.usp_GastoDirecto_PartidasDisponibles;
DROP PROCEDURE IF EXISTS contable.usp_GastoDirecto_ProveedoresPorSeccion;
DROP PROCEDURE IF EXISTS ControlPresupuestario.usp_CatalogoPartida_Listar;
DROP PROCEDURE IF EXISTS ControlPresupuestario.usp_CatalogoPartida_Obtener;
DROP PROCEDURE IF EXISTS ControlPresupuestario.usp_CatalogoPartida_Crear;
DROP PROCEDURE IF EXISTS ControlPresupuestario.usp_CatalogoPartida_Actualizar;
DROP PROCEDURE IF EXISTS ControlPresupuestario.usp_CatalogoPartida_CargaMasiva;
DROP PROCEDURE IF EXISTS ControlPresupuestario.usp_SeccionGasto_Listar;

-- No eliminar silenciosamente consumidores desconocidos del TVP.
IF TYPE_ID(N'ControlPresupuestario.TVP_CatalogoPartida') IS NOT NULL
   AND EXISTS
   (
       SELECT 1
       FROM sys.parameters p
       WHERE p.user_type_id = TYPE_ID(N'ControlPresupuestario.TVP_CatalogoPartida')
   )
    THROW 51670, 'DEPENDENCIA_INESPERADA: existe un objeto no reconocido que usa TVP_CatalogoPartida.', 1;

IF TYPE_ID(N'ControlPresupuestario.TVP_CatalogoPartida') IS NOT NULL
    DROP TYPE ControlPresupuestario.TVP_CatalogoPartida;

EXEC(N'CREATE TYPE ControlPresupuestario.TVP_CatalogoPartida AS TABLE
(
    Codigo          VARCHAR(50)   NOT NULL,
    Nombre          NVARCHAR(200) NOT NULL,
    IdTipoPartida   INT           NOT NULL,
    CodigoPadre     VARCHAR(50)   NULL,
    Descripcion     NVARCHAR(500) NULL,
    _Fila           INT           NOT NULL
)');

-- Primero se retiran las tablas puente, que son consumidores conocidos.
DROP TABLE IF EXISTS ControlPresupuestario.SeccionGastoCategoriaGasto;
DROP TABLE IF EXISTS ControlPresupuestario.SeccionGastoTipoCentroCosto;

IF COL_LENGTH(N'ControlPresupuestario.CatalogoPartida', N'IdSeccionGasto') IS NOT NULL
BEGIN
    -- Solo se aceptan la FK y el índice creados por V2.5.0.
    IF EXISTS
    (
        SELECT 1
        FROM sys.foreign_key_columns fkc
        JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
        WHERE fkc.parent_object_id = OBJECT_ID(N'ControlPresupuestario.CatalogoPartida')
          AND fkc.parent_column_id = COLUMNPROPERTY(
              OBJECT_ID(N'ControlPresupuestario.CatalogoPartida'), N'IdSeccionGasto', 'ColumnId')
          AND fk.name <> N'FK_CatalogoPartida_SeccionGasto'
    )
        THROW 51671, 'DEPENDENCIA_INESPERADA: IdSeccionGasto tiene una FK no reconocida.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM sys.index_columns ic
        JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        WHERE ic.object_id = OBJECT_ID(N'ControlPresupuestario.CatalogoPartida')
          AND ic.column_id = COLUMNPROPERTY(
              OBJECT_ID(N'ControlPresupuestario.CatalogoPartida'), N'IdSeccionGasto', 'ColumnId')
          AND i.name <> N'IX_CatalogoPartida_IdSeccionGasto'
    )
        THROW 51672, 'DEPENDENCIA_INESPERADA: IdSeccionGasto tiene un índice no reconocido.', 1;

    IF EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'ControlPresupuestario.CatalogoPartida')
                 AND name = N'FK_CatalogoPartida_SeccionGasto')
        ALTER TABLE ControlPresupuestario.CatalogoPartida
            DROP CONSTRAINT FK_CatalogoPartida_SeccionGasto;

    IF EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'ControlPresupuestario.CatalogoPartida')
                 AND name = N'IX_CatalogoPartida_IdSeccionGasto')
        DROP INDEX IX_CatalogoPartida_IdSeccionGasto
            ON ControlPresupuestario.CatalogoPartida;

    ALTER TABLE ControlPresupuestario.CatalogoPartida DROP COLUMN IdSeccionGasto;
END;

-- Tras retirar los consumidores conocidos, una FK restante indica una extensión
-- ajena a esta migración y debe revisarse manualmente.
IF OBJECT_ID(N'ControlPresupuestario.SeccionGasto', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE referenced_object_id = OBJECT_ID(N'ControlPresupuestario.SeccionGasto')
   )
    THROW 51673, 'DEPENDENCIA_INESPERADA: SeccionGasto conserva una FK no reconocida.', 1;

DROP TABLE IF EXISTS ControlPresupuestario.SeccionGasto;

COMMIT TRANSACTION;
GO
