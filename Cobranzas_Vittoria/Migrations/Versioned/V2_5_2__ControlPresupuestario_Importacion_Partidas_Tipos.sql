/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.5.2
Módulo      : Control Presupuestario
Descripción : Tipo tabla para la importación masiva del catálogo de partidas.
===============================================================================

El orden de columnas DEBE coincidir con el de las propiedades públicas de
CatalogoPartidaImportTvpDto (TvpMapper respeta el orden de declaración).

Tipo y sección llegan ya resueltos a Id por la API; el padre viaja por código
porque puede ser otra fila del mismo archivo, todavía sin Id.
===============================================================================
*/

IF TYPE_ID(N'ControlPresupuestario.TVP_CatalogoPartida') IS NULL
    CREATE TYPE ControlPresupuestario.TVP_CatalogoPartida AS TABLE
    (
        Codigo          VARCHAR(50)   NOT NULL,
        Nombre          NVARCHAR(200) NOT NULL,
        IdTipoPartida   INT           NOT NULL,
        CodigoPadre     VARCHAR(50)   NULL,
        IdSeccionGasto  INT           NULL,
        Descripcion     NVARCHAR(500) NULL,
        _Fila           INT           NOT NULL
    );
GO
