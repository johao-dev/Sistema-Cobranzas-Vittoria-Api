ALTER TABLE seguridad.Rol
    ADD Descripcion nvarchar(255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL;
GO

ALTER TABLE seguridad.Rol
    ADD UsuarioCreacion nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL;
GO

ALTER TABLE seguridad.Rol
    ADD FechaModificacion datetime2(0) NULL;
GO

ALTER TABLE seguridad.Rol
    ADD UsuarioModificacion nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL;
GO

-- SQL Server requiere sp_rename para renombrar columnas.
EXEC sp_rename 'seguridad.Rol.NombreRol', 'Nombre', 'COLUMN';
GO
