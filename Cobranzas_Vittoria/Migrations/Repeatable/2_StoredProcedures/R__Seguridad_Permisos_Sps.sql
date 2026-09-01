-- Procedimiento para obtener un permiso por su identificador.
CREATE OR ALTER PROCEDURE seguridad.usp_Permiso_GetById
    @IdPermiso INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        IdPermiso,
        Codigo,
        Nombre,
        Descripcion,
        Activo,
        FechaCreacion,
        UsuarioCreacion,
        FechaModificacion,
        UsuarioModificacion
    FROM seguridad.Permiso
    WHERE IdPermiso = @IdPermiso;
END;
GO

-- Procedimiento para listar permisos filtrados por estado activo.
CREATE OR ALTER PROCEDURE seguridad.usp_Permiso_List
    @Activo BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        IdPermiso,
        Codigo,
        Nombre,
        Descripcion,
        Activo,
        FechaCreacion,
        UsuarioCreacion,
        FechaModificacion,
        UsuarioModificacion
    FROM seguridad.Permiso
    WHERE Activo = @Activo
    ORDER BY Nombre;
END;
GO

-- Procedimiento para insertar un nuevo permiso en la tabla seguridad.Permiso y devolver sus detalles.
CREATE OR ALTER PROCEDURE seguridad.usp_Permiso_Insert
    @Codigo NVARCHAR(100),
    @Nombre NVARCHAR(100),
    @Descripcion NVARCHAR(255),
    @UsuarioCreacion NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Id INT;

    INSERT INTO seguridad.Permiso (Codigo, Nombre, Descripcion, FechaCreacion, UsuarioCreacion)
    VALUES (@Codigo, @Nombre, @Descripcion, GETUTCDATE(), @UsuarioCreacion);

    SET @Id = CAST(SCOPE_IDENTITY() AS INT);

    SELECT
        IdPermiso,
        Codigo,
        Nombre,
        Descripcion,
        Activo,
        FechaCreacion,
        UsuarioCreacion,
        FechaModificacion,
        UsuarioModificacion
    FROM seguridad.Permiso
    WHERE IdPermiso = @Id;
END;
GO

-- Procedimiento para actualizar un permiso existente.
-- Solo se actualizan Nombre, Descripcion, Activo y auditoria de modificacion.
CREATE OR ALTER PROCEDURE seguridad.usp_Permiso_Update
    @IdPermiso INT,
    @Nombre NVARCHAR(100),
    @Descripcion NVARCHAR(255),
    @Activo BIT,
    @FechaModificacion DATETIME = NULL,
    @UsuarioModificacion NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE seguridad.Permiso
    SET
        Nombre = @Nombre,
        Descripcion = @Descripcion,
        Activo = @Activo,
        FechaModificacion = ISNULL(@FechaModificacion, GETUTCDATE()),
        UsuarioModificacion = @UsuarioModificacion
    WHERE IdPermiso = @IdPermiso;

    SELECT
        IdPermiso,
        Codigo,
        Nombre,
        Descripcion,
        Activo,
        FechaCreacion,
        UsuarioCreacion,
        FechaModificacion,
        UsuarioModificacion
    FROM seguridad.Permiso
    WHERE IdPermiso = @IdPermiso;
END;
GO

-- Procedimiento para eliminar fisicamente un permiso.
CREATE OR ALTER PROCEDURE seguridad.usp_Permiso_Delete
    @IdPermiso INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM seguridad.Permiso
    WHERE IdPermiso = @IdPermiso;
END;
GO