/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.5.8
Módulo      : Control Presupuestario
Descripción : Tipo tabla para la importación masiva de centros de costo.
===============================================================================

El orden de columnas DEBE coincidir con el de las propiedades públicas de
CentroCostoImportTvpDto (TvpMapper respeta el orden de declaración).

Tipo y proyecto llegan ya resueltos a Id por la API.
===============================================================================
*/

IF TYPE_ID(N'ControlPresupuestario.TVP_CentroCosto') IS NULL
    CREATE TYPE ControlPresupuestario.TVP_CentroCosto AS TABLE
    (
        Codigo              VARCHAR(30)   NOT NULL,
        Nombre              NVARCHAR(150) NOT NULL,
        IdTipoCentroCosto   INT           NOT NULL,
        IdProyecto          INT           NULL,
        Descripcion         NVARCHAR(255) NULL,
        _Fila               INT           NOT NULL
    );
GO
