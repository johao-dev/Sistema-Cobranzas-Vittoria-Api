-- Añade registro de auditoría a la tabla Usuario

ALTER TABLE seguridad.Usuario
ADD FechaModificacion datetime2(0) NULL;
GO

ALTER TABLE seguridad.Usuario
ADD UsuarioModificacion nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL;
GO