/*
Migración: 1.4.3

Descripción:
Aumenta el tamaño del campo Codigo de seguridad.Permiso de nvarchar(24) a nvarchar(128),
agrega la constraint UNIQUE para garantizar unicidad de códigos y vuelve a crear el
índice agrupado si fuera necesario.
*/

ALTER TABLE seguridad.Permiso
ALTER COLUMN Codigo NVARCHAR(128) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL;

-- Garantiza unicidad de código a nivel de base de datos.
-- Si ya existiera una constraint con otro nombre, primero la elimina.
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('seguridad.Permiso')
      AND name = 'UQ_Permiso_Codigo'
)
    ALTER TABLE seguridad.Permiso DROP CONSTRAINT UQ_Permiso_Codigo;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('seguridad.Permiso')
      AND is_unique = 1
      AND index_id > 0
)
    ALTER TABLE seguridad.Permiso
    ADD CONSTRAINT UQ_Permiso_Codigo UNIQUE (Codigo);
