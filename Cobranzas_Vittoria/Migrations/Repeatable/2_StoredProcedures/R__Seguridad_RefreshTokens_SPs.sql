-- =============================================
-- Author:      Johao Bravo
-- Create date: 2026-09-08
-- Description: Procedimientos almacenados para la gestión de refresh tokens en el sistema.
-- =============================================

-- =============================================
-- Idempotencia: Asegura que los procedimientos almacenados se puedan crear o eliminar sin errores si ya existen o no.
-- =============================================

IF OBJECT_ID('seguridad.usp_RefreshToken_Insert', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_RefreshToken_Insert;
GO

IF OBJECT_ID('seguridad.usp_RefreshToken_GetByHash', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_RefreshToken_GetByHash;
GO

IF OBJECT_ID('seguridad.usp_RefreshToken_Revoke', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_RefreshToken_Revoke;
GO

-- =============================================
-- Procedimientos Almacenados relacionados con la tabla seguridad.RefreshToken
-- =============================================

-- Procedimiento para insertar un nuevo refresh token
CREATE OR ALTER PROCEDURE seguridad.usp_RefreshToken_Insert
    @IdUsuario INT,
    @TokenHash CHAR(64),
    @FechaExpiracionUtc DATETIME2(0),
    @FechaCreacionUtc DATETIME2(0)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO seguridad.RefreshToken (IdUsuario, TokenHash, FechaExpiracionUtc, FechaCreacionUtc)
    VALUES (@IdUsuario, @TokenHash, @FechaExpiracionUtc, @FechaCreacionUtc);
END;
GO

-- Procedimiento para obtener un refresh token por su hash
CREATE OR ALTER PROCEDURE seguridad.usp_RefreshToken_GetByHash
    @TokenHash CHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdRefreshToken, IdUsuario, TokenHash, FechaExpiracionUtc, FechaCreacionUtc,
           FechaRevocacionUtc, ReemplazadoPorHash
    FROM seguridad.RefreshToken
    WHERE TokenHash = @TokenHash;
END;
GO

-- Procedimiento para revocar un refresh token
CREATE OR ALTER PROCEDURE seguridad.usp_RefreshToken_Revoke
    @IdRefreshToken INT,
    @FechaRevocacionUtc DATETIME2(0),
    @ReemplazadoPorHash CHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE seguridad.RefreshToken
    SET FechaRevocacionUtc = @FechaRevocacionUtc,
        ReemplazadoPorHash = @ReemplazadoPorHash
    WHERE IdRefreshToken = @IdRefreshToken
      AND FechaRevocacionUtc IS NULL;
END;
GO
