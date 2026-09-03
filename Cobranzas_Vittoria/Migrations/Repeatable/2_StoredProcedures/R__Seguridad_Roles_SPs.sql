-- =============================================
-- Author:      Johao Bravo
-- Create date: 2026-09-02
-- Description: Procedimientos almacenados para la gestión de roles en el sistema.
-- =============================================

-- Este SP es legacy, se elimina una vez y no se volvera a crear.
-- Asi que esta comprobación solo funcionara la primera vez.
IF OBJECT_ID('seguridad.usp_Rol_Upsert', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Rol_Upsert;
GO

-- =============================================
-- Idempotencia: Asegura que los procedimientos almacenados se puedan crear o eliminar sin errores si ya existen o no.
-- =============================================

IF OBJECT_ID('seguridad.usp_Rol_GetById', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Rol_GetById;
GO

IF OBJECT_ID('seguridad.usp_Rol_GetByNombre', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Rol_GetByNombre;
GO

IF OBJECT_ID('seguridad.usp_Rol_List', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Rol_List;
GO

IF OBJECT_ID('seguridad.usp_Rol_Insert', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Rol_Insert;
GO

IF OBJECT_ID('seguridad.usp_Rol_Update', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Rol_Update;
GO

IF OBJECT_ID('seguridad.usp_Rol_Delete', 'P') IS NOT NULL
    DROP PROCEDURE seguridad.usp_Rol_Delete;
GO

-- =============================================
-- Procedimientos Almacenados relacionados con la tabla seguridad.Rol
-- =============================================

-- Procedimiento almacenado para obtener un rol por su identificador.
CREATE OR ALTER PROCEDURE seguridad.usp_Rol_GetById
    @IdRol INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        IdRol,
        Nombre,
        Descripcion,
        Activo,
        FechaCreacion,
        UsuarioCreacion,
        FechaModificacion,
        UsuarioModificacion
    FROM
        seguridad.Rol
    WHERE
        IdRol = @IdRol;
END;
GO

-- Procedimiento para obtener un rol por su nombre
CREATE OR ALTER PROCEDURE seguridad.usp_Rol_GetByNombre
    @Nombre NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        IdRol,
        Nombre,
        Descripcion,
        Activo,
        FechaCreacion,
        UsuarioCreacion,
        FechaModificacion,
        UsuarioModificacion
    FROM seguridad.Rol
    WHERE Nombre = @Nombre;
END;
GO

-- Procedimiento para listar roles filtrados por estado activo.
CREATE OR ALTER PROCEDURE seguridad.usp_Rol_List
    @Activo BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT
        IdRol,
        Nombre,
        Descripcion,
        Activo,
        FechaCreacion,
        UsuarioCreacion,
        FechaModificacion,
        UsuarioModificacion
    FROM seguridad.Rol
    WHERE (@Activo IS NULL OR Activo = @Activo)
    ORDER BY Nombre;
END;
GO

-- Procedimiento para insertar un nuevo en la tabla seguridad.Rol y devolver sus detalles.
CREATE OR ALTER PROCEDURE seguridad.usp_Rol_Insert
    @Nombre NVARCHAR(100),
    @Descripcion NVARCHAR(255) = NULL,
    @Activo BIT = 1,
    @UsuarioCreacion NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Id INT;

    INSERT INTO seguridad.Rol (Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion)
    VALUES (@Nombre, @Descripcion, @Activo, SYSDATETIME(), @UsuarioCreacion);

    SET @Id = CAST(SCOPE_IDENTITY() AS INT);

    SELECT
        IdRol,
        Nombre,
        Descripcion,
        Activo,
        FechaCreacion,
        UsuarioCreacion,
        FechaModificacion,
        UsuarioModificacion
    FROM seguridad.Rol
    WHERE IdRol = @Id;
END;
GO

-- Procedimiento para actualizar un rol existente.
CREATE OR ALTER PROCEDURE seguridad.usp_Rol_Update
    @IdRol INT,
    @Nombre NVARCHAR(100),
    @Descripcion NVARCHAR(255),
    @Activo BIT,
    @FechaModificacion DATETIME = NULL,
    @UsuarioModificacion NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE seguridad.Rol
    SET
        Nombre = @Nombre,
        Descripcion = @Descripcion,
        Activo = @Activo,
        FechaModificacion = ISNULL(@FechaModificacion, SYSDATETIME()),
        UsuarioModificacion = @UsuarioModificacion
    WHERE IdRol = @IdRol;

    SELECT
        IdRol,
        Nombre,
        Descripcion,
        Activo,
        FechaCreacion,
        UsuarioCreacion,
        FechaModificacion,
        UsuarioModificacion
    FROM seguridad.Rol
    WHERE IdRol = @IdRol;
END;
GO

-- Procedimiento para eliminar fisiacmente un rol.
-- Nota: Considerar si este SP debería existir.
CREATE OR ALTER PROCEDURE seguridad.usp_Rol_Delete
    @IdRol INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM seguridad.Rol
    WHERE IdRol = @IdRol;
END;
GO