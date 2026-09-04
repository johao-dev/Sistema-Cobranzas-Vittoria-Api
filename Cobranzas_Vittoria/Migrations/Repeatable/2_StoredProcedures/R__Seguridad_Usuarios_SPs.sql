-- =============================================
-- Author:      Johao Bravo
-- Create date: 2026-09-03
-- Description: Procedimientos almacenados para la gestión de usuarios en el sistema.
-- =============================================

-- Ests SPs son legacy, se eliminan una vez y no se volveran a crear.
IF OBJECT_ID('seguridad.usp_Usuario_Get', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Usuario_Get;
GO

IF OBJECT_ID('seguridad.usp_Usuario_Upsert', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Usuario_Upsert;
GO

IF OBJECT_ID('seguridad.usp_UsuarioRol_Asignar', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_UsuarioRol_Asignar;
GO

IF OBJECT_ID('seguridad.usp_UsuarioRol_Quitar', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_UsuarioRol_Quitar;
GO

-- =============================================
-- Idempotencia: Asegura que los procedimientos almacenados se puedan crear o eliminar sin errores si ya existen o no.
-- =============================================

IF OBJECT_ID('seguridad.usp_Usuario_GetById', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Usuario_GetById;
GO

/*
TODO: Idea de refactor futuro
Considerar tener mejor un SP tipo: seguridad.usp_Usuario_SearchByCriteria
El cual puede buscar por ID, correo y nombres + apellidos completos.
De esa forma se centraliza la lógica de búsqueda y se evita tener múltiples procedimientos
almacenados para criterios similares.
*/

IF OBJECT_ID('seguridad.usp_Usuario_GetByCorreo', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Usuario_GetByCorreo;
GO

IF OBJECT_ID('seguridad.usp_Usuario_List', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Usuario_List;
GO

IF OBJECT_ID('seguridad.usp_Usuario_Insert', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Usuario_Insert;
GO

IF OBJECT_ID('seguridad.usp_Usuario_Update', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Usuario_Update;
GO

IF OBJECT_ID('seguridad.usp_Usuario_AsignarRoles', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Usuario_AsignarRoles;
GO

IF OBJECT_ID('seguridad.usp_Usuario_QuitarRol', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Usuario_QuitarRol;
GO

-- =============================================
-- Procedimientos Almacenados relacionados con la tabla seguridad.Usuario
-- =============================================

-- Procedimiento almacenado para obtener un usuario por su ID
CREATE OR ALTER PROCEDURE seguridad.usp_Usuario_GetById
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        IdUsuario,
        Nombres,
        Apellidos,
        Correo,
        UsuarioLogin,
        PasswordHash,
        Activo,
        FechaCreacion,
        UsuarioCreacion
    FROM seguridad.Usuario
    WHERE IdUsuario = @UsuarioId;
END;
GO

-- Procedimiento para obtener un usuario por su correo electrónico
CREATE OR ALTER PROCEDURE seguridad.usp_Usuario_GetByCorreo
    @Correo NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        IdUsuario,
        Nombres,
        Apellidos,
        Correo,
        UsuarioLogin,
        PasswordHash,
        Activo,
        FechaCreacion,
        UsuarioCreacion
    FROM seguridad.Usuario
    WHERE Correo = @Correo;
END;
GO

-- Procedimiento para listar todos los usuarios filtrados por su estado activo
CREATE OR ALTER PROCEDURE seguridad.usp_Usuario_List
    @Activo BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        IdUsuario,
        Nombres,
        Apellidos,
        Correo,
        UsuarioLogin,
        PasswordHash,
        Activo,
        FechaCreacion,
        UsuarioCreacion
    FROM seguridad.Usuario
    WHERE @Activo IS NULL OR Activo = @Activo;
END;
GO

-- Procedimiento para agregar un nuevo usuario
CREATE OR ALTER PROCEDURE seguridad.usp_Usuario_Insert
    @Nombres NVARCHAR(255),
    @Apellidos NVARCHAR(255),
    @Correo NVARCHAR(255),
    @UsuarioLogin NVARCHAR(255),
    @PasswordHash NVARCHAR(255),
    @Activo BIT = 1,
    @UsuarioCreacion NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO seguridad.Usuario (
        Nombres,
        Apellidos,
        Correo,
        UsuarioLogin,
        PasswordHash,
        Activo,
        FechaCreacion,
        UsuarioCreacion)
    VALUES (
        @Nombres,
        @Apellidos,
        @Correo,
        @UsuarioLogin,
        @PasswordHash,
        @Activo,
        GETDATE(),
        @UsuarioCreacion);
END;
GO

-- Procedimiento para actualizar los datos editables de un usuario
-- TODO: La tabla Usuario quizá deba tener campos de auditoría de modificación
CREATE OR ALTER PROCEDURE seguridad.usp_Usuario_Update
    @IdUsuario INT,
    @Nombres NVARCHAR(255),
    @Apellidos NVARCHAR(255),
    @Correo NVARCHAR(255),
    @UsuarioLogin NVARCHAR(255),
    @PasswordHash NVARCHAR(255),
    @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE seguridad.Usuario
    SET
        Nombres = @Nombres,
        Apellidos = @Apellidos,
        Correo = @Correo,
        UsuarioLogin = @UsuarioLogin,
        PasswordHash = @PasswordHash,
        Activo = @Activo
    WHERE IdUsuario = @IdUsuario;
END;
GO

-- Procedimiento para asignar 1 o más roles a un usuario
CREATE OR ALTER PROCEDURE seguridad.usp_Usuario_AsignarRoles
    @UsuarioId INT,
    @Roles NVARCHAR(MAX) -- Lista de IDs de roles separados por comas
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON; -- Si ocurre un error de ejecución, cancela la transacción de inmediato

    -- 1. Validación rápida de parámetros de entrada
    IF @UsuarioId IS NULL OR @Roles IS NULL OR LTRIM(RTRIM(@Roles)) = ''
        RETURN;

    -- 2. Validar que el usuario exista y esté activo
    IF NOT EXISTS (SELECT 1 FROM seguridad.Usuario WHERE IdUsuario = @UsuarioId)
    BEGIN
        RAISERROR('El usuario especificado no existe.', 16, 1);
        RETURN;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Extraer, limpiar, castear y descartar duplicados internos de la cadena
        WITH RolesParsed AS (
            SELECT DISTINCT 
                TRY_CAST(TRIM(value) AS INT) AS RolId
            FROM STRING_SPLIT(@Roles, ',')
            WHERE TRIM(value) <> ''
        )
        INSERT INTO seguridad.UsuarioRol (IdUsuario, IdRol)
        SELECT 
            @UsuarioId, 
            rp.RolId
        FROM RolesParsed rp
        -- Validar que el rol exista en el catálogo de roles
        INNER JOIN seguridad.Rol r 
            ON r.IdRol = rp.RolId
        -- Evitar colisiones si el usuario ya tiene ese rol asignado
        WHERE NOT EXISTS (
            SELECT 1 
            FROM seguridad.UsuarioRol ur 
            WHERE ur.IdUsuario = @UsuarioId 
              AND ur.IdRol = rp.RolId
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW; -- Relanza el error original hacia la API
    END CATCH;
END;
GO

-- Procedimiento para quitar un rol a un usuario
CREATE OR ALTER PROCEDURE seguridad.usp_Usuario_QuitarRol
    @UsuarioId INT,
    @RolId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Validación defensiva
    IF @UsuarioId IS NULL OR @RolId IS NULL
    BEGIN
        RAISERROR('UsuarioId y RolId son obligatorios.', 16, 1);
        RETURN;
    END;

    BEGIN TRY
        DELETE FROM seguridad.UsuarioRol
        WHERE IdUsuario = @UsuarioId
          AND IdRol = @RolId;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END;
GO