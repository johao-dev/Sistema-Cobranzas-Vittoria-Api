-- Agrega columna de auditoria en la tabla UsuarioRol

ALTER TABLE seguridad.UsuarioRol
ADD UsuarioCreacion nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL;
GO