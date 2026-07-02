CREATE TABLE contable.CotizacionMaterialEspecialidad (
	IdCotizacionMaterialEspecialidad int IDENTITY(1,1) NOT NULL,
	IdProyecto int NOT NULL,
	IdEspecialidad int NOT NULL,
	Cotizacion decimal(18,2) DEFAULT 0 NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime DEFAULT getdate() NOT NULL,
	FechaActualizacion datetime NULL,
	CONSTRAINT PK_CotizacionMaterialEspecialidad PRIMARY KEY (IdCotizacionMaterialEspecialidad)
);
 CREATE NONCLUSTERED INDEX IX_CotizacionMaterialEspecialidad_Proyecto_Especialidad ON contable.CotizacionMaterialEspecialidad (  IdProyecto ASC  , IdEspecialidad ASC  , Activo ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;
 CREATE UNIQUE NONCLUSTERED INDEX UX_CotizacionMaterialEspecialidad_ProyectoEspecialidad ON contable.CotizacionMaterialEspecialidad (  IdProyecto ASC  , IdEspecialidad ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;
 CREATE UNIQUE NONCLUSTERED INDEX UX_CotizacionMaterialEspecialidad_Proyecto_Especialidad_Activo ON contable.CotizacionMaterialEspecialidad (  IdProyecto ASC  , IdEspecialidad ASC  )  
	 WHERE  ([Activo]=(1) AND [IdProyecto] IS NOT NULL)
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE contable.GastoAdministrativo (
	IdGastoAdministrativo int IDENTITY(1,1) NOT NULL,
	IdCategoriaGasto int NOT NULL,
	IdProveedor int NULL,
	Fecha date NOT NULL,
	Monto decimal(18,2) NOT NULL,
	Descripcion nvarchar(500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Moneda nvarchar(10) COLLATE SQL_Latin1_General_CP1_CI_AS DEFAULT 'PEN' NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT getdate() NOT NULL,
	IdProveedorGastoAdministrativo int NULL,
	IdProyecto int NULL,
	CONSTRAINT PK__GastoAdm__0FDF3C2DA234FF78 PRIMARY KEY (IdGastoAdministrativo)
);
 CREATE NONCLUSTERED INDEX IX_GastoAdministrativo_IdProyecto ON contable.GastoAdministrativo (  IdProyecto ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE contable.GastoAdministrativoDocumento (
	IdGastoAdministrativoDocumento int IDENTITY(1,1) NOT NULL,
	IdGastoAdministrativo int NOT NULL,
	TipoDocumento nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	NombreArchivo nvarchar(255) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	RutaArchivo nvarchar(400) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Extension nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion datetime2(0) DEFAULT getdate() NOT NULL,
	CONSTRAINT PK__GastoAdm__41AD2D1BD512C312 PRIMARY KEY (IdGastoAdministrativoDocumento)
);
ALTER TABLE contable.GastoAdministrativoDocumento WITH NOCHECK ADD CONSTRAINT CK_GastoAdministrativoDocumento_TipoDocumento CHECK (([TipoDocumento]='Pago' OR [TipoDocumento]='Factura'));

CREATE TABLE contable.GastoProyecto (
	IdGastoProyecto int IDENTITY(1,1) NOT NULL,
	TipoModulo nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	IdProyecto int NOT NULL,
	IdProveedorTerreno int NULL,
	Fecha date NOT NULL,
	Concepto nvarchar(150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Moneda nvarchar(10) COLLATE SQL_Latin1_General_CP1_CI_AS DEFAULT 'PEN' NOT NULL,
	MontoSoles decimal(18,2) DEFAULT 0 NOT NULL,
	MontoDolares decimal(18,2) DEFAULT 0 NOT NULL,
	FechaTipoCambio date NULL,
	TipoCambio decimal(18,4) DEFAULT 3.4100 NOT NULL,
	Descripcion nvarchar(500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Estado nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS DEFAULT 'Activo' NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime DEFAULT getdate() NOT NULL,
	FechaActualizacion datetime NULL,
	CONSTRAINT PK_GastoProyecto PRIMARY KEY (IdGastoProyecto)
);
 CREATE NONCLUSTERED INDEX IX_GastoProyecto_ModuloProyecto ON contable.GastoProyecto (  TipoModulo ASC  , IdProyecto ASC  , Estado ASC  , Activo ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;
ALTER TABLE contable.GastoProyecto WITH NOCHECK ADD CONSTRAINT CK_GastoProyecto_TipoModulo CHECK (([TipoModulo]='GastosMunicipales' OR [TipoModulo]='OtrosGastos' OR [TipoModulo]='Marketing' OR [TipoModulo]='Terreno'));
ALTER TABLE contable.GastoProyecto WITH NOCHECK ADD CONSTRAINT CK_GastoProyecto_Estado CHECK (([Estado]='Inactivo' OR [Estado]='Activo'));

CREATE TABLE contable.GastoProyectoDocumento (
	IdGastoProyectoDocumento int IDENTITY(1,1) NOT NULL,
	IdGastoProyecto int NOT NULL,
	TipoDocumento nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS DEFAULT 'Factura' NOT NULL,
	NombreArchivo nvarchar(255) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	RutaArchivo nvarchar(500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Extension nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion datetime DEFAULT getdate() NOT NULL,
	CONSTRAINT PK_GastoProyectoDocumento PRIMARY KEY (IdGastoProyectoDocumento)
);
 CREATE NONCLUSTERED INDEX IX_GastoProyectoDocumento_Gasto ON contable.GastoProyectoDocumento (  IdGastoProyecto ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE contable.PresupuestoProyecto (
	IdPresupuestoProyecto int IDENTITY(1,1) NOT NULL,
	IdProyecto int NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	FechaActualizacion datetime2(0) NULL,
	CONSTRAINT PK_PresupuestoProyecto PRIMARY KEY (IdPresupuestoProyecto),
	CONSTRAINT UQ_PresupuestoProyecto_IdProyecto UNIQUE (IdProyecto)
);
 CREATE UNIQUE NONCLUSTERED INDEX UX_PresupuestoProyecto_IdProyecto ON contable.PresupuestoProyecto (  IdProyecto ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE contable.PresupuestoProyectoDetalle (
	IdPresupuestoProyectoDetalle int IDENTITY(1,1) NOT NULL,
	IdPresupuestoProyecto int NOT NULL,
	Orden int NOT NULL,
	Concepto nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Soles decimal(18,2) NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	Dolares decimal(18,2) DEFAULT 0 NOT NULL,
	CONSTRAINT PK_PresupuestoProyectoDetalle PRIMARY KEY (IdPresupuestoProyectoDetalle)
);
 CREATE NONCLUSTERED INDEX IX_PresupuestoProyectoDetalle_IdPresupuestoProyecto ON contable.PresupuestoProyectoDetalle (  IdPresupuestoProyecto ASC  , Orden ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE contable.Terreno (
	IdTerreno int IDENTITY(1,1) NOT NULL,
	Fecha date DEFAULT CONVERT([date],getdate()) NOT NULL,
	IdProyecto int NOT NULL,
	Terreno nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Alcabala decimal(18,2) DEFAULT 0 NOT NULL,
	Estado nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS DEFAULT 'Activo' NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	FechaActualizacion datetime2(0) NULL,
	CONSTRAINT PK_Terreno PRIMARY KEY (IdTerreno)
);
 CREATE NONCLUSTERED INDEX IX_Terreno_IdProyecto ON contable.Terreno (  IdProyecto ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE contable.Valorizacion (
	IdValorizacion int IDENTITY(1,1) NOT NULL,
	NumeroValorizacion nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	IdProyecto int NULL,
	IdProveedor int NOT NULL,
	IdEspecialidad int NOT NULL,
	IdProveedorEspecialidadCotizacion int NULL,
	Empresa nvarchar(200) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Servicio nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Moneda nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS DEFAULT 'Soles' NOT NULL,
	Cotizacion decimal(18,2) NOT NULL,
	PorcentajeGarantia decimal(9,6) DEFAULT 0.050000 NOT NULL,
	PorcentajeDetraccion decimal(9,6) DEFAULT 0.040000 NOT NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK__Valoriza__F6FB61312A9DA469 PRIMARY KEY (IdValorizacion)
);

CREATE TABLE contable.ValorizacionDetalle (
	IdValorizacionDetalle int IDENTITY(1,1) NOT NULL,
	IdValorizacion int NOT NULL,
	FechaFactura date NULL,
	NumeroFactura nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	MontoFactura decimal(18,2) NOT NULL,
	Descripcion nvarchar(500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	PorcentajeDetraccionAplicado decimal(9,6) NOT NULL,
	PorcentajeGarantiaAplicado decimal(9,6) NOT NULL,
	OtrosDescuentos decimal(18,2) DEFAULT 0 NOT NULL,
	FechaTransferencia date NULL,
	NumeroOperacion nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	BancoTransferencia nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	BancoDestino nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	MontoTransferido decimal(18,2) DEFAULT 0 NOT NULL,
	BaseImponible AS (CONVERT([decimal](18,2),round([MontoFactura]/(1.18),(2)))) PERSISTED,
	Igv AS (CONVERT([decimal](18,2),round([MontoFactura]-round([MontoFactura]/(1.18),(2)),(2)))) PERSISTED,
	MontoDetraccion AS (CONVERT([decimal](18,2),round([MontoFactura]*[PorcentajeDetraccionAplicado],(2)))) PERSISTED,
	MontoGarantia AS (CONVERT([decimal](18,2),round([MontoFactura]*[PorcentajeGarantiaAplicado],(2)))) PERSISTED,
	MontoAbonar AS (CONVERT([decimal](18,2),round((([MontoFactura]-[MontoFactura]*[PorcentajeDetraccionAplicado])-[MontoFactura]*[PorcentajeGarantiaAplicado])-[OtrosDescuentos],(2)))) PERSISTED,
	MontoAFavor AS (CONVERT([decimal](18,2),case when [MontoTransferido]>round((([MontoFactura]-[MontoFactura]*[PorcentajeDetraccionAplicado])-[MontoFactura]*[PorcentajeGarantiaAplicado])-[OtrosDescuentos],(2)) then round([MontoTransferido]-round((([MontoFactura]-[MontoFactura]*[PorcentajeDetraccionAplicado])-[MontoFactura]*[PorcentajeGarantiaAplicado])-[OtrosDescuentos],(2)),(2)) else (0) end)) PERSISTED,
	MontoDeuda AS (CONVERT([decimal](18,2),case when round((([MontoFactura]-[MontoFactura]*[PorcentajeDetraccionAplicado])-[MontoFactura]*[PorcentajeGarantiaAplicado])-[OtrosDescuentos],(2))>[MontoTransferido] then round(round((([MontoFactura]-[MontoFactura]*[PorcentajeDetraccionAplicado])-[MontoFactura]*[PorcentajeGarantiaAplicado])-[OtrosDescuentos],(2))-[MontoTransferido],(2)) else (0) end)) PERSISTED,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	TipoDetraccion nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	IdProveedorEspecialidadCotizacion int NULL,
	CONSTRAINT PK__Valoriza__48D9D80E0A5B1991 PRIMARY KEY (IdValorizacionDetalle)
);
 CREATE NONCLUSTERED INDEX IX_ValorizacionDetalle_IdProveedorEspecialidadCotizacion ON contable.ValorizacionDetalle (  IdProveedorEspecialidadCotizacion ASC  )  
	 INCLUDE ( Activo , FechaFactura , IdValorizacion , IdValorizacionDetalle ) 
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE contable.ValorizacionDetalleArchivo (
	IdValorizacionDetalleArchivo int IDENTITY(1,1) NOT NULL,
	IdValorizacionDetalle int NOT NULL,
	NombreArchivo nvarchar(260) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	RutaArchivo nvarchar(500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Extension nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion datetime DEFAULT getdate() NOT NULL,
	CONSTRAINT PK__Valoriza__3A054B96D0D79D32 PRIMARY KEY (IdValorizacionDetalleArchivo)
);

ALTER TABLE contable.CotizacionMaterialEspecialidad ADD CONSTRAINT FK_CotizacionMaterialEspecialidad_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES maestra.Especialidad(IdEspecialidad);
ALTER TABLE contable.CotizacionMaterialEspecialidad ADD CONSTRAINT FK_CotizacionMaterialEspecialidad_Proyecto FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto);

ALTER TABLE contable.GastoAdministrativo ADD CONSTRAINT FK_GastoAdministrativo_CategoriaGasto FOREIGN KEY (IdCategoriaGasto) REFERENCES maestra.CategoriaGasto(IdCategoriaGasto);
ALTER TABLE contable.GastoAdministrativo ADD CONSTRAINT FK_GastoAdministrativo_Proveedor FOREIGN KEY (IdProveedor) REFERENCES maestra.Proveedor(IdProveedor);
ALTER TABLE contable.GastoAdministrativo ADD CONSTRAINT FK_GastoAdministrativo_ProveedorGastoAdministrativo FOREIGN KEY (IdProveedorGastoAdministrativo) REFERENCES maestra.ProveedorGastoAdministrativo(IdProveedorGastoAdministrativo);
ALTER TABLE contable.GastoAdministrativo ADD CONSTRAINT FK_GastoAdministrativo_Proyecto FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto);

ALTER TABLE contable.GastoAdministrativoDocumento ADD CONSTRAINT FK_GastoAdministrativoDocumento_GastoAdministrativo FOREIGN KEY (IdGastoAdministrativo) REFERENCES contable.GastoAdministrativo(IdGastoAdministrativo);

ALTER TABLE contable.GastoProyecto ADD CONSTRAINT FK_GastoProyecto_ProveedorTerreno FOREIGN KEY (IdProveedorTerreno) REFERENCES maestra.ProveedorTerreno(IdProveedorTerreno);
ALTER TABLE contable.GastoProyecto ADD CONSTRAINT FK_GastoProyecto_Proyecto FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto);

ALTER TABLE contable.GastoProyectoDocumento ADD CONSTRAINT FK_GastoProyectoDocumento_GastoProyecto FOREIGN KEY (IdGastoProyecto) REFERENCES contable.GastoProyecto(IdGastoProyecto);

ALTER TABLE contable.PresupuestoProyecto ADD CONSTRAINT FK_PresupuestoProyecto_Proyecto FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto);

ALTER TABLE contable.PresupuestoProyectoDetalle ADD CONSTRAINT FK_PresupuestoProyectoDetalle_PresupuestoProyecto FOREIGN KEY (IdPresupuestoProyecto) REFERENCES contable.PresupuestoProyecto(IdPresupuestoProyecto);

ALTER TABLE contable.Terreno ADD CONSTRAINT FK_Terreno_Proyecto FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto);

ALTER TABLE contable.Valorizacion ADD CONSTRAINT FK_Valorizacion_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES maestra.Especialidad(IdEspecialidad);
ALTER TABLE contable.Valorizacion ADD CONSTRAINT FK_Valorizacion_PECotizacion FOREIGN KEY (IdProveedorEspecialidadCotizacion) REFERENCES maestra.ProveedorEspecialidadCotizacion(IdProveedorEspecialidadCotizacion);
ALTER TABLE contable.Valorizacion ADD CONSTRAINT FK_Valorizacion_Proveedor FOREIGN KEY (IdProveedor) REFERENCES maestra.Proveedor(IdProveedor);
ALTER TABLE contable.Valorizacion ADD CONSTRAINT FK_Valorizacion_Proyecto FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto);

ALTER TABLE contable.ValorizacionDetalle ADD CONSTRAINT FK_ValDet_Valorizacion FOREIGN KEY (IdValorizacion) REFERENCES contable.Valorizacion(IdValorizacion);

ALTER TABLE contable.ValorizacionDetalleArchivo ADD CONSTRAINT FK_ValorizacionDetalleArchivo_Detalle FOREIGN KEY (IdValorizacionDetalle) REFERENCES contable.ValorizacionDetalle(IdValorizacionDetalle);
