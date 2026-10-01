/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.6.1
Módulo      : Control Presupuestario
Descripción : Auditoría de la anulación de versiones.
===============================================================================

El contrato de API exige un motivo al anular una versión en BORRADOR. La versión
guarda quién la anuló, cuándo y por qué, igual que ya guarda la aprobación.
Las columnas son opcionales: las versiones anuladas antes de esta migración no
tienen esos datos.
===============================================================================
*/

IF COL_LENGTH('ControlPresupuestario.PresupuestoVersion', 'MotivoAnulacion') IS NULL
    ALTER TABLE ControlPresupuestario.PresupuestoVersion ADD MotivoAnulacion NVARCHAR(500) NULL;
GO

IF COL_LENGTH('ControlPresupuestario.PresupuestoVersion', 'FechaAnulacion') IS NULL
    ALTER TABLE ControlPresupuestario.PresupuestoVersion ADD FechaAnulacion DATETIME2(0) NULL;
GO

IF COL_LENGTH('ControlPresupuestario.PresupuestoVersion', 'UsuarioAnulacion') IS NULL
    ALTER TABLE ControlPresupuestario.PresupuestoVersion ADD UsuarioAnulacion NVARCHAR(100) NULL;
GO
