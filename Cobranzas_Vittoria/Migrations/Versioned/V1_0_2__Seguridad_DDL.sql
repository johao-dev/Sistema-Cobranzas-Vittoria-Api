CREATE TABLE VittoriaComprasDB_New.seguridad.Rol (
	IdRol int IDENTITY(1,1) NOT NULL,
	NombreRol nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK__Rol__2A49584C14043C66 PRIMARY KEY (IdRol)
);

CREATE TABLE VittoriaComprasDB_New.seguridad.Usuario (
	IdUsuario int IDENTITY(1,1) NOT NULL,
	Nombres nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Apellidos nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Correo nvarchar(150) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	UsuarioLogin nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	PasswordHash nvarchar(500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	UsuarioCreacion nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CONSTRAINT PK__Usuario__5B65BF97283C953C PRIMARY KEY (IdUsuario)
);
 CREATE UNIQUE NONCLUSTERED INDEX UX_Usuario_UsuarioLogin ON VittoriaComprasDB_New.seguridad.Usuario (  UsuarioLogin ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE VittoriaComprasDB_New.seguridad.UsuarioRol (
	IdUsuarioRol int IDENTITY(1,1) NOT NULL,
	IdUsuario int NOT NULL,
	IdRol int NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK__UsuarioR__6806BF4A621300AB PRIMARY KEY (IdUsuarioRol),
	CONSTRAINT FK_UsuarioRol_Rol FOREIGN KEY (IdRol) REFERENCES VittoriaComprasDB_New.seguridad.Rol(IdRol),
	CONSTRAINT FK_UsuarioRol_Usuario FOREIGN KEY (IdUsuario) REFERENCES VittoriaComprasDB_New.seguridad.Usuario(IdUsuario)
);
