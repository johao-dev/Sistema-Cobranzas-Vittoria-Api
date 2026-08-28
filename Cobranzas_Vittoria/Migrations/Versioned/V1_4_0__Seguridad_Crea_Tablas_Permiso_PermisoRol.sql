/*
Migración Versionada: 1.4.0

Descripción:
Crea las tablas de seguridad para la gestión de permisos (Permiso)
y la asignación de permisos a roles (PermisoRol), estableciendo
las relaciones necesarias con la tabla Rol.
*/

CREATE TABLE seguridad.Permiso (
    IdPermiso int IDENTITY(1,1) NOT NULL,
    Codigo nvarchar(24) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    Nombre nvarchar(64) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    Descripcion nvarchar(255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
    FechaCreacion datetime2(0) NULL,
    UsuarioCreacion nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
    FechaModificacion datetime2(0) NULL,
    UsuarioModificacion nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,

    CONSTRAINT PK_Permiso PRIMARY KEY (IdPermiso)
);

CREATE TABLE seguridad.PermisoRol (
    IdPermisoRol int IDENTITY(1,1) NOT NULL,
    IdPermiso int NOT NULL,
    IdRol int NOT NULL,
    FechaCreacion datetime2(0) NULL,
    UsuarioCreacion nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,

    CONSTRAINT PK_PermisoRol PRIMARY KEY (IdPermisoRol),
    CONSTRAINT UQ_PermisoRol UNIQUE (IdPermiso, IdRol),
    CONSTRAINT FK_PermisoRol_Permiso FOREIGN KEY (IdPermiso) REFERENCES seguridad.Permiso(IdPermiso),
    CONSTRAINT FK_PermisoRol_Rol FOREIGN KEY (IdRol) REFERENCES seguridad.Rol(IdRol)
);