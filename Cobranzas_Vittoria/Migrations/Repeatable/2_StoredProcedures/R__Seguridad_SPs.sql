CREATE OR ALTER PROCEDURE [seguridad].[usp_Rol_List]
    @Activo BIT = NULL
AS
BEGIN
    SET
    NOCOUNT ON;
    
    SELECT
    IdRol,
    NombreRol,
    Activo,
    FechaCreacion
FROM
    seguridad.Rol
WHERE
    (@Activo IS NULL
        OR Activo = @Activo)
ORDER BY
    NombreRol;
END;
GO

CREATE OR ALTER PROCEDURE [seguridad].[usp_Rol_Upsert]
    @IdRol INT = NULL,
    @NombreRol NVARCHAR(100),
    @Activo BIT
AS
BEGIN
    SET
NOCOUNT ON;

SET
@NombreRol = LTRIM(RTRIM(ISNULL(@NombreRol, '')));

IF @NombreRol = ''
        THROW 50001,
'Debes ingresar el nombre del rol.',
1;

IF EXISTS (
SELECT
    1
FROM
    seguridad.Rol
WHERE
    NombreRol = @NombreRol
    AND (@IdRol IS NULL
        OR IdRol <> @IdRol)
    )
        THROW 50001,
'Ya existe un rol con ese nombre.',
1;

IF @IdRol IS NULL
OR @IdRol = 0
    BEGIN
        INSERT
    INTO
    seguridad.Rol (NombreRol,
    Activo,
    FechaCreacion)
VALUES (@NombreRol,
@Activo,
SYSDATETIME());

SELECT
    CAST(SCOPE_IDENTITY() AS INT);

RETURN;
END

    UPDATE
    seguridad.Rol
SET
    NombreRol = @NombreRol,
           Activo = @Activo
WHERE
    IdRol = @IdRol;

SELECT
    @IdRol;
END;
GO

CREATE OR ALTER PROCEDURE [seguridad].[usp_Usuario_Get]
    @IdUsuario INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
    u.IdUsuario,
            u.Nombres,
            u.Apellidos,
            u.Correo,
            u.UsuarioLogin,
            u.Activo,
            u.FechaCreacion,
            u.UsuarioCreacion
FROM
    seguridad.Usuario u
WHERE
    u.IdUsuario = @IdUsuario;

SELECT
    ur.IdUsuarioRol,
            ur.IdUsuario,
            ur.IdRol,
            r.NombreRol
FROM
    seguridad.UsuarioRol ur
INNER JOIN seguridad.Rol r ON
    r.IdRol = ur.IdRol
WHERE
    ur.IdUsuario = @IdUsuario
ORDER BY
    r.NombreRol;
END;
GO

CREATE OR ALTER PROCEDURE [seguridad].[usp_Usuario_List]
    @Activo BIT = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
    u.IdUsuario,
            u.Nombres,
            u.Apellidos,
            u.Correo,
            u.UsuarioLogin,
            u.Activo,
            u.FechaCreacion
FROM
    seguridad.Usuario u
WHERE
    (@Activo IS NULL
        OR u.Activo = @Activo)
ORDER BY
    u.Nombres,
    u.Apellidos;
END;
GO

CREATE OR ALTER PROCEDURE [seguridad].[usp_Usuario_Upsert]
    @IdUsuario INT = NULL,
    @Nombres NVARCHAR(100),
    @Apellidos NVARCHAR(100) = NULL,
    @Correo NVARCHAR(150) = NULL,
    @UsuarioLogin NVARCHAR(100),
    @PasswordHash NVARCHAR(500),
    @Activo BIT = 1,
    @UsuarioCreacion NVARCHAR(100) = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SET
XACT_ABORT ON;

IF NULLIF(LTRIM(RTRIM(@Nombres)), '') IS NULL
        THROW 50001,
'Nombres es requerido.',
1;

IF NULLIF(LTRIM(RTRIM(@UsuarioLogin)), '') IS NULL
        THROW 50002,
'UsuarioLogin es requerido.',
1;

IF @IdUsuario IS NULL
    BEGIN
        IF EXISTS (
SELECT
    1
FROM
    seguridad.Usuario
WHERE
    UsuarioLogin = @UsuarioLogin)
            THROW 50003,
'Ya existe un usuario con ese login.',
1;

INSERT
    INTO
    seguridad.Usuario
        (
            Nombres,
    Apellidos,
    Correo,
    UsuarioLogin,
    PasswordHash,
    Activo,
    UsuarioCreacion
        )
VALUES
        (
            @Nombres,
@Apellidos,
@Correo,
@UsuarioLogin,
@PasswordHash,
@Activo,
@UsuarioCreacion
        );

SELECT
    CAST(SCOPE_IDENTITY() AS INT) AS IdUsuario;
END
ELSE
    BEGIN
        IF EXISTS (
SELECT
1
FROM
seguridad.Usuario
WHERE
UsuarioLogin = @UsuarioLogin
AND IdUsuario <> @IdUsuario)
            THROW 50004,
'Ya existe otro usuario con ese login.',
1;

UPDATE
    seguridad.Usuario
SET
    Nombres = @Nombres,
               Apellidos = @Apellidos,
               Correo = @Correo,
               UsuarioLogin = @UsuarioLogin,
               PasswordHash = @PasswordHash,
               Activo = @Activo
WHERE
    IdUsuario = @IdUsuario;

SELECT
    @IdUsuario AS IdUsuario;
END
END;
GO

CREATE OR ALTER PROCEDURE [seguridad].[usp_UsuarioRol_Asignar]
    @IdUsuario INT,
    @IdRol INT
AS
BEGIN
    SET
NOCOUNT ON;

IF NOT EXISTS (
SELECT
    1
FROM
    seguridad.Usuario
WHERE
    IdUsuario = @IdUsuario)
        THROW 50005,
'Usuario no existe.',
1;

IF NOT EXISTS (
SELECT
    1
FROM
    seguridad.Rol
WHERE
    IdRol = @IdRol)
        THROW 50006,
'Rol no existe.',
1;

IF NOT EXISTS (
SELECT
    1
FROM
    seguridad.UsuarioRol
WHERE
    IdUsuario = @IdUsuario
    AND IdRol = @IdRol)
    BEGIN
        INSERT
    INTO
    seguridad.UsuarioRol(IdUsuario,
    IdRol)
VALUES (@IdUsuario,
@IdRol);
END

    SELECT
    1 AS Ok;
END;
GO

CREATE OR ALTER PROCEDURE [seguridad].[usp_UsuarioRol_Quitar]
    @IdUsuario INT,
    @IdRol INT
AS
BEGIN
    SET
NOCOUNT ON;

DELETE
FROM
    seguridad.UsuarioRol
WHERE
    IdUsuario = @IdUsuario
    AND IdRol = @IdRol;

SELECT
    1 AS Ok;
END;
