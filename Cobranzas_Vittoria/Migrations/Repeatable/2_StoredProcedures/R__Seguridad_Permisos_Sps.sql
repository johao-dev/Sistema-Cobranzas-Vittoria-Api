
-- Procedimiento para insertar un nuevo permiso en la tabla seguridad.Permiso y devolver sus detalles.
CREATE OR ALTER PROCEDURE seguridad.usp_Permiso_Insert
    @Codigo NVARCHAR(100),
    @Nombre NVARCHAR(100),
    @Descripcion NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Id INT;

    INSERT INTO seguridad.Permiso (Codigo, Nombre, Descripcion)
    VALUES (@Codigo, @Nombre, @Descripcion);

    SET @Id = CAST(SCOPE_IDENTITY() AS INT);

    SELECT
        IdPermiso,
        Codigo,
        Nombre,
        Descripcion,
        Activo,
        FechaCreacion,
        UsuarioCreacion
    FROM seguridad.Permiso
    WHERE IdPermiso = @Id;
END;