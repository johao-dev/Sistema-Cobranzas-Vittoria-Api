/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.5.4
Módulo      : Maestra / Control Presupuestario
Descripción : Partida presupuestal por defecto de cada material.
===============================================================================

Cada material puede tener una partida del catálogo (una hoja). Al agregarlo a
un requerimiento, la partida se propone sola buscándola en la versión APROBADA
del presupuesto del proyecto; el residente puede cambiarla.

NULL = el material no tiene partida por defecto (se elige a mano, como antes).

TVP_Material_v3: igual que v2 más IdCatalogoPartida. Se crea un tipo nuevo en
vez de alterar v2 (SQL Server no altera tipos tabla en uso); v2 y su SP quedan
intactos para no romper integraciones existentes.

Reentrante: cada objeto valida su propia ausencia.
===============================================================================
*/

IF COL_LENGTH(N'maestra.Material', N'IdCatalogoPartida') IS NULL
    ALTER TABLE maestra.Material
        ADD IdCatalogoPartida INT NULL
            CONSTRAINT FK_Material_CatalogoPartida
            REFERENCES ControlPresupuestario.CatalogoPartida (IdCatalogoPartida);
GO

-- Sin filtro a propósito: en SQL Server 2025 un índice filtrado sobre la columna
-- de una FK impide compilar el plan del DELETE sobre la tabla referenciada (Msg 8624).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Material_IdCatalogoPartida' AND object_id = OBJECT_ID(N'maestra.Material')
)
    CREATE NONCLUSTERED INDEX IX_Material_IdCatalogoPartida
    ON maestra.Material (IdCatalogoPartida);
GO

IF TYPE_ID(N'maestra.TVP_Material_v3') IS NULL
    CREATE TYPE maestra.TVP_Material_v3 AS TABLE
    (
        IdEspecialidad     INT            NOT NULL,
        Codigo             NVARCHAR(50)   NOT NULL,
        Descripcion        NVARCHAR(200)  NOT NULL,
        IdUnidadMedida     INT            NULL,
        UnidadMedida       NVARCHAR(30)   NOT NULL,
        IdCatalogoPartida  INT            NULL,
        _Fila              INT            NOT NULL
    );
GO
