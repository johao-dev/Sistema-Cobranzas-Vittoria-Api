/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.0.0
Módulo      : Control Presupuestario
Descripción : Creación del schema ControlPresupuestario.
Autor       : Johao Bravo
===============================================================================
*/

IF NOT EXISTS
(
    SELECT 1
    FROM sys.schemas
    WHERE NAME = 'ControlPresupuestario'
)
BEGIN
    EXEC('CREATE SCHEMA ControlPresupuestario');
END;
GO