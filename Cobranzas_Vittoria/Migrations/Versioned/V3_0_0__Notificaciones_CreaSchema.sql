/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V3.0.0
Módulo      : Notificaciones
Descripción : Creación del schema Notificaciones.
Autor       : Johao Bravo
===============================================================================
*/

IF NOT EXISTS
(
    SELECT 1
    FROM sys.schemas
    WHERE NAME = 'Notificaciones'
)
BEGIN
    EXEC('CREATE SCHEMA Notificaciones');
END;
GO

/*
Nota:
Estas migraciones aún no han sido aplicadas, revisar el código antes de ejecutar.
*/