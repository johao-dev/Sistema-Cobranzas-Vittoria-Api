-- =============================================
-- Author:      Johao Bravo
-- Create date: 2026-09-08
-- Description: Procedimientos almacenados para la gestión del flujo de requerimientos de compras.
-- =============================================

/*
El antiguo usp_Requerimiento_ActualizarEstado aceptaba un estado libre y hacía
posible saltar etapas. Se elimina y se reemplaza por un SP por transición.

Deuda técnica planificada: mientras Estado sea texto, estos SP son la barrera
transaccional de las transiciones. En una evolución posterior se normalizarán
estados/transiciones e historial, y el dominio aplicará el patrón State.
*/

-- Obsoleto, eliminado definitivamente.
IF OBJECT_ID('compras.usp_Requerimiento_ActualizarEstado', 'P') IS NOT NULL
    DROP PROCEDURE compras.usp_Requerimiento_ActualizarEstado;
GO

-- =============================================
-- Idempotencia: Asegura que los procedimientos almacenados se puedan crear o eliminar sin errores si ya existen o no.
-- =============================================

IF OBJECT_ID('compras.usp_Requerimiento_Enviar', 'P') IS NOT NULL
    DROP PROCEDURE compras.usp_Requerimiento_Enviar;
GO

IF OBJECT_ID('compras.usp_Requerimiento_ProcesarStock', 'P') IS NOT NULL
    DROP PROCEDURE compras.usp_Requerimiento_ProcesarStock;
GO

IF OBJECT_ID('compras.usp_Requerimiento_Aprobar', 'P') IS NOT NULL
    DROP PROCEDURE compras.usp_Requerimiento_Aprobar;
GO

IF OBJECT_ID('compras.usp_Requerimiento_Rechazar', 'P') IS NOT NULL
    DROP PROCEDURE compras.usp_Requerimiento_Rechazar;
GO

IF OBJECT_ID('compras.usp_Requerimiento_EnviarCompras', 'P') IS NOT NULL
    DROP PROCEDURE compras.usp_Requerimiento_EnviarCompras;
GO

-- =============================================
-- Procedimientos Almacenados relacionados con la tabla compras.Requerimiento
-- =============================================

-- Procedimiento para enviar un requerimiento
CREATE OR ALTER PROCEDURE compras.usp_Requerimiento_Enviar
    @IdRequerimiento INT,
    @IdUsuario INT,
    @Observacion NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM compras.Requerimiento WHERE IdRequerimiento = @IdRequerimiento)
        THROW 51051, 'El requerimiento no existe.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM compras.Requerimiento
        WHERE IdRequerimiento = @IdRequerimiento
          AND IdUsuarioSolicitante = @IdUsuario
          AND Estado = N'Registrado')
        THROW 51052, 'Solo el solicitante puede enviar un requerimiento registrado.', 1;

    UPDATE compras.Requerimiento
    SET Estado = N'EnviadoAlmacen', Observacion = COALESCE(@Observacion, Observacion)
    WHERE IdRequerimiento = @IdRequerimiento;
END;
GO

-- Procedimiento para procesar el stock de un requerimiento
CREATE OR ALTER PROCEDURE compras.usp_Requerimiento_ProcesarStock
    @IdRequerimiento INT,
    @IdUsuario INT,
    @Resultado NVARCHAR(20),
    @Observacion NVARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Resultado NOT IN (N'Conforme', N'Observado')
        THROW 51053, 'El resultado de almacén es inválido.', 1;

    IF NOT EXISTS (SELECT 1 FROM seguridad.Usuario WHERE IdUsuario = @IdUsuario)
        THROW 51054, 'El usuario que procesa el requerimiento no existe.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM compras.Requerimiento
        WHERE IdRequerimiento = @IdRequerimiento AND Estado = N'EnviadoAlmacen')
        THROW 51055, 'El requerimiento debe estar enviado a almacén para procesar stock.', 1;

    BEGIN TRANSACTION;

    INSERT INTO compras.RequerimientoValidacion (IdRequerimiento, IdUsuario, Resultado, Observacion)
    VALUES (@IdRequerimiento, @IdUsuario, @Resultado, @Observacion);

    UPDATE compras.Requerimiento
    SET Estado = CASE WHEN @Resultado = N'Conforme' THEN N'ValidadoAlmacen' ELSE N'Registrado' END,
        Observacion = COALESCE(@Observacion, Observacion)
    WHERE IdRequerimiento = @IdRequerimiento;

    COMMIT TRANSACTION;
END;
GO

-- Procedimiento para aprobar un requerimiento
CREATE OR ALTER PROCEDURE compras.usp_Requerimiento_Aprobar
    @IdRequerimiento INT,
    @IdUsuario INT,
    @Observacion NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM seguridad.Usuario WHERE IdUsuario = @IdUsuario)
        THROW 51054, 'El usuario que aprueba el requerimiento no existe.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM compras.Requerimiento
        WHERE IdRequerimiento = @IdRequerimiento AND Estado = N'ValidadoAlmacen')
        THROW 51056, 'Solo puede aprobarse un requerimiento validado por almacén.', 1;

    UPDATE compras.Requerimiento
    SET Estado = N'AprobadoCoordinador', Observacion = COALESCE(@Observacion, Observacion)
    WHERE IdRequerimiento = @IdRequerimiento;
END;
GO

-- Procedimiento para rechazar un requerimiento
CREATE OR ALTER PROCEDURE compras.usp_Requerimiento_Rechazar
    @IdRequerimiento INT,
    @IdUsuario INT,
    @Observacion NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM seguridad.Usuario WHERE IdUsuario = @IdUsuario)
        THROW 51054, 'El usuario que rechaza el requerimiento no existe.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM compras.Requerimiento
        WHERE IdRequerimiento = @IdRequerimiento AND Estado IN (N'ValidadoAlmacen', N'AprobadoCoordinador'))
        THROW 51057, 'Solo puede rechazarse un requerimiento revisado por coordinación.', 1;

    UPDATE compras.Requerimiento
    SET Estado = N'Rechazado', Observacion = COALESCE(@Observacion, Observacion)
    WHERE IdRequerimiento = @IdRequerimiento;
END;
GO

-- Procedimiento para enviar un requerimiento a compras
CREATE OR ALTER PROCEDURE compras.usp_Requerimiento_EnviarCompras
    @IdRequerimiento INT,
    @IdUsuario INT,
    @Observacion NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM seguridad.Usuario WHERE IdUsuario = @IdUsuario)
        THROW 51054, 'El usuario que envía el requerimiento a compras no existe.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM compras.Requerimiento
        WHERE IdRequerimiento = @IdRequerimiento AND Estado = N'AprobadoCoordinador')
        THROW 51058, 'Solo puede enviarse a compras un requerimiento aprobado por coordinación.', 1;

    UPDATE compras.Requerimiento
    SET Estado = N'EnviadoOC', Observacion = COALESCE(@Observacion, Observacion)
    WHERE IdRequerimiento = @IdRequerimiento;
END;
GO
