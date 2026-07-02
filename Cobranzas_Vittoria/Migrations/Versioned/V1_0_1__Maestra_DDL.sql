CREATE TABLE maestra.CategoriaGasto (
	IdCategoriaGasto int IDENTITY(1,1) NOT NULL,
	Nombre nvarchar(150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT getdate() NOT NULL,
	CONSTRAINT PK__Categori__59627481C0E32FF2 PRIMARY KEY (IdCategoriaGasto)
);

CREATE TABLE maestra.Especialidad (
	IdEspecialidad int IDENTITY(1,1) NOT NULL,
	Nombre nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Descripcion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK__Especial__693FA0AF9704F656 PRIMARY KEY (IdEspecialidad)
);

CREATE TABLE maestra.Proveedor (
	IdProveedor int IDENTITY(1,1) NOT NULL,
	RazonSocial nvarchar(200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Ruc nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Contacto nvarchar(150) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Telefono nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Correo nvarchar(150) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Direccion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Banco nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CuentaCorriente nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CCI nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CuentaDetraccion nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	DescripcionServicio nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	TrabajamosConProveedor nvarchar(10) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK__Proveedo__E8B631AF147286D1 PRIMARY KEY (IdProveedor)
);

CREATE TABLE maestra.ProveedorTerreno (
	IdProveedorTerreno int IDENTITY(1,1) NOT NULL,
	RazonSocial nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Ruc nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Contacto nvarchar(150) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Telefono nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Correo nvarchar(150) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime DEFAULT getdate() NOT NULL,
	CONSTRAINT PK_ProveedorTerreno PRIMARY KEY (IdProveedorTerreno)
);
 CREATE NONCLUSTERED INDEX IX_ProveedorTerreno_RazonSocial ON maestra.ProveedorTerreno (  RazonSocial ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE maestra.Proyecto (
	IdProyecto int IDENTITY(1,1) NOT NULL,
	NombreProyecto nvarchar(150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Descripcion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CotizacionGeneral decimal(18,2) DEFAULT 0 NOT NULL,
	CONSTRAINT PK__Proyecto__F4888673455DC373 PRIMARY KEY (IdProyecto)
);

CREATE TABLE maestra.UnidadMedida (
	IdUnidadMedida int IDENTITY(1,1) NOT NULL,
	Codigo nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Nombre nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime DEFAULT getdate() NOT NULL,
	CONSTRAINT PK__UnidadMe__18F83A9395471AEA PRIMARY KEY (IdUnidadMedida)
);

CREATE TABLE maestra.Material (
	IdMaterial int IDENTITY(1,1) NOT NULL,
	IdEspecialidad int NOT NULL,
	Codigo nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Descripcion nvarchar(200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	UnidadMedida nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	StockMinimo decimal(18,2) DEFAULT 0 NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	IdUnidadMedida int NULL,
	CodigoProveedor varchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CONSTRAINT PK__Material__94356E58EF19AC75 PRIMARY KEY (IdMaterial),
	CONSTRAINT FK_Material_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES maestra.Especialidad(IdEspecialidad),
	CONSTRAINT FK_Material_UnidadMedida FOREIGN KEY (IdUnidadMedida) REFERENCES maestra.UnidadMedida(IdUnidadMedida)
);

CREATE TABLE maestra.ProveedorEspecialidad (
	IdProveedorEspecialidad int IDENTITY(1,1) NOT NULL,
	IdProveedor int NOT NULL,
	IdEspecialidad int NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK__Proveedo__6D334D81828772DA PRIMARY KEY (IdProveedorEspecialidad),
	CONSTRAINT FK_ProveedorEspecialidad_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES maestra.Especialidad(IdEspecialidad),
	CONSTRAINT FK_ProveedorEspecialidad_Proveedor FOREIGN KEY (IdProveedor) REFERENCES maestra.Proveedor(IdProveedor)
);

CREATE TABLE maestra.ProveedorEspecialidadCotizacion (
	IdProveedorEspecialidadCotizacion int IDENTITY(1,1) NOT NULL,
	IdProyecto int NULL,
	IdProveedor int NOT NULL,
	IdEspecialidad int NOT NULL,
	Servicio nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Moneda nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS DEFAULT 'Soles' NOT NULL,
	MontoCotizacion decimal(18,2) NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	Empresa nvarchar(200) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CONSTRAINT PK__Proveedo__2CAA567EC1E119DB PRIMARY KEY (IdProveedorEspecialidadCotizacion),
	CONSTRAINT FK_PECotizacion_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES maestra.Especialidad(IdEspecialidad),
	CONSTRAINT FK_PECotizacion_Proveedor FOREIGN KEY (IdProveedor) REFERENCES maestra.Proveedor(IdProveedor),
	CONSTRAINT FK_PECotizacion_Proyecto FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto)
);

CREATE TABLE maestra.ProveedorGastoAdministrativo (
	IdProveedorGastoAdministrativo int IDENTITY(1,1) NOT NULL,
	RazonSocial nvarchar(200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Ruc nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Contacto nvarchar(120) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Telefono nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Correo nvarchar(150) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT getdate() NOT NULL,
	IdCategoriaGasto int NULL,
	CONSTRAINT PK__Proveedo__2D142B016D6EE1D0 PRIMARY KEY (IdProveedorGastoAdministrativo),
	CONSTRAINT FK_ProveedorGastoAdministrativo_CategoriaGasto FOREIGN KEY (IdCategoriaGasto) REFERENCES maestra.CategoriaGasto(IdCategoriaGasto)
);
 CREATE NONCLUSTERED INDEX IX_ProveedorGastoAdministrativo_Categoria_Activo ON maestra.ProveedorGastoAdministrativo (  IdCategoriaGasto ASC  , Activo ASC  , RazonSocial ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;
 CREATE UNIQUE NONCLUSTERED INDEX UX_ProveedorGastoAdministrativo_RazonSocial ON maestra.ProveedorGastoAdministrativo (  RazonSocial ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;
 CREATE UNIQUE NONCLUSTERED INDEX UX_ProveedorGastoAdministrativo_Ruc ON maestra.ProveedorGastoAdministrativo (  Ruc ASC  )  
	 WHERE  ([Ruc] IS NOT NULL)
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE maestra.ProveedorReglaValorizacion (
	IdProveedorReglaValorizacion int IDENTITY(1,1) NOT NULL,
	IdProveedor int NOT NULL,
	PorcentajeGarantia decimal(9,6) DEFAULT 0.050000 NOT NULL,
	PorcentajeDetraccion decimal(9,6) DEFAULT 0.040000 NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK__Proveedo__A138DF6D351C3154 PRIMARY KEY (IdProveedorReglaValorizacion),
	CONSTRAINT FK_ProveedorReglaVal_Proveedor FOREIGN KEY (IdProveedor) REFERENCES maestra.Proveedor(IdProveedor)
);
