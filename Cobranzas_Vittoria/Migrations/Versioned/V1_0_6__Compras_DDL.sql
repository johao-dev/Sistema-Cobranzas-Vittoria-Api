CREATE TABLE VittoriaComprasDB_New.compras.Compra (
	IdCompra int IDENTITY(1,1) NOT NULL,
	NumeroCompra nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	IdOrdenCompra int NOT NULL,
	IdProveedor int NOT NULL,
	FechaCompra date NOT NULL,
	Aceptada bit DEFAULT 0 NOT NULL,
	MontoTotal decimal(18,2) DEFAULT 0 NOT NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	IncluyeIGV bit DEFAULT 0 NOT NULL,
	SubtotalSinIGV decimal(18,2) DEFAULT 0 NOT NULL,
	MontoIGV decimal(18,2) DEFAULT 0 NOT NULL,
	CONSTRAINT PK__Compra__0A5CDB5CCB2FF022 PRIMARY KEY (IdCompra)
);

CREATE TABLE VittoriaComprasDB_New.compras.CompraDetalle (
	IdCompraDetalle int IDENTITY(1,1) NOT NULL,
	IdCompra int NOT NULL,
	IdMaterial int NOT NULL,
	Cantidad decimal(18,2) NOT NULL,
	PrecioUnitario decimal(18,2) DEFAULT 0 NOT NULL,
	Subtotal AS (round([Cantidad]*[PrecioUnitario],(2))) PERSISTED,
	CONSTRAINT PK__CompraDe__A1B840C57799EF06 PRIMARY KEY (IdCompraDetalle)
);

CREATE TABLE VittoriaComprasDB_New.compras.CompraDocumento (
	IdCompraDocumento int IDENTITY(1,1) NOT NULL,
	IdCompra int NOT NULL,
	TipoDocumento nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	NumeroDocumento nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	RutaArchivo nvarchar(500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	FechaDocumento date NULL,
	Monto decimal(18,2) NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	NombreArchivo nvarchar(260) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Extension nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion datetime DEFAULT getdate() NOT NULL,
	CONSTRAINT PK__CompraDo__D75313A666A2DC58 PRIMARY KEY (IdCompraDocumento)
);
ALTER TABLE VittoriaComprasDB_New.compras.CompraDocumento WITH NOCHECK ADD CONSTRAINT CK_CompraDocumento_Tipo CHECK (([TipoDocumento]=N'Pago' OR [TipoDocumento]=N'GuiaRemision' OR [TipoDocumento]=N'Factura'));

CREATE TABLE VittoriaComprasDB_New.compras.OrdenCompra (
	IdOrdenCompra int IDENTITY(1,1) NOT NULL,
	NumeroOrdenCompra nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	IdRequerimiento int NOT NULL,
	IdProveedor int NOT NULL,
	IdProyecto int NOT NULL,
	FechaOrdenCompra date NOT NULL,
	Descripcion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Estado nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Total decimal(18,2) DEFAULT 0 NOT NULL,
	RutaPdf nvarchar(300) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	IdUsuarioCreacion int NULL,
	CONSTRAINT PK__OrdenCom__685E464B9250BA33 PRIMARY KEY (IdOrdenCompra)
);
ALTER TABLE VittoriaComprasDB_New.compras.OrdenCompra WITH NOCHECK ADD CONSTRAINT CK_OrdenCompra_Estado CHECK (([Estado]='Anulada' OR [Estado]='Cerrada' OR [Estado]='Atendida' OR [Estado]='Aceptada' OR [Estado]='Registrada'));

CREATE TABLE VittoriaComprasDB_New.compras.OrdenCompraDetalle (
	IdOrdenCompraDetalle int IDENTITY(1,1) NOT NULL,
	IdOrdenCompra int NOT NULL,
	IdMaterial int NOT NULL,
	Cantidad decimal(18,2) NOT NULL,
	PrecioUnitario decimal(18,2) DEFAULT 0 NOT NULL,
	Subtotal AS (round([Cantidad]*[PrecioUnitario],(2))) PERSISTED,
	IdProveedor int NOT NULL,
	CONSTRAINT PK__OrdenCom__B9A159106BB80A82 PRIMARY KEY (IdOrdenCompraDetalle)
);

CREATE TABLE VittoriaComprasDB_New.compras.OrdenCompraHistorial (
	IdOrdenCompraHistorial int IDENTITY(1,1) NOT NULL,
	IdOrdenCompra int NOT NULL,
	EstadoAnterior nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	EstadoNuevo nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	FechaCambio datetime2(0) DEFAULT sysdatetime() NOT NULL,
	IdUsuario int NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CONSTRAINT PK__OrdenCom__6DA255062B68DE6A PRIMARY KEY (IdOrdenCompraHistorial)
);

CREATE TABLE VittoriaComprasDB_New.compras.Requerimiento (
	IdRequerimiento int IDENTITY(1,1) NOT NULL,
	NumeroRequerimiento nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	FechaRequerimiento date NOT NULL,
	IdEspecialidad int NOT NULL,
	IdProyecto int NOT NULL,
	Descripcion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaEntrega date NULL,
	IdUsuarioSolicitante int NOT NULL,
	Estado nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK__Requerim__BAFD1D03571E04F3 PRIMARY KEY (IdRequerimiento)
);
ALTER TABLE VittoriaComprasDB_New.compras.Requerimiento WITH NOCHECK ADD CONSTRAINT CK_Requerimiento_Estado CHECK (([Estado]='Anulado' OR [Estado]='GeneradoOC' OR [Estado]='EnviadoOC' OR [Estado]='ValidadoAlmacen' OR [Estado]='Registrado'));

CREATE TABLE VittoriaComprasDB_New.compras.RequerimientoDetalle (
	IdRequerimientoDetalle int IDENTITY(1,1) NOT NULL,
	IdRequerimiento int NOT NULL,
	IdMaterial int NOT NULL,
	Cantidad decimal(18,2) NOT NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CONSTRAINT PK__Requerim__B616BCD8731DF384 PRIMARY KEY (IdRequerimientoDetalle)
);

CREATE TABLE VittoriaComprasDB_New.compras.RequerimientoValidacion (
	IdRequerimientoValidacion int IDENTITY(1,1) NOT NULL,
	IdRequerimiento int NOT NULL,
	IdUsuario int NOT NULL,
	FechaValidacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	Resultado nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CONSTRAINT PK__Requerim__F81A90383E701012 PRIMARY KEY (IdRequerimientoValidacion)
);

ALTER TABLE VittoriaComprasDB_New.compras.RequerimientoValidacion WITH NOCHECK ADD CONSTRAINT CK_RequerimientoValidacion_Resultado CHECK (([Resultado]=N'Observado' OR [Resultado]=N'Conforme'));

ALTER TABLE VittoriaComprasDB_New.compras.Compra ADD CONSTRAINT FK_Compra_OrdenCompra FOREIGN KEY (IdOrdenCompra) REFERENCES VittoriaComprasDB_New.compras.OrdenCompra(IdOrdenCompra);
ALTER TABLE VittoriaComprasDB_New.compras.Compra ADD CONSTRAINT FK_Compra_Proveedor FOREIGN KEY (IdProveedor) REFERENCES VittoriaComprasDB_New.maestra.Proveedor(IdProveedor);

ALTER TABLE VittoriaComprasDB_New.compras.CompraDetalle ADD CONSTRAINT FK_CompraDetalle_Compra FOREIGN KEY (IdCompra) REFERENCES VittoriaComprasDB_New.compras.Compra(IdCompra);
ALTER TABLE VittoriaComprasDB_New.compras.CompraDetalle ADD CONSTRAINT FK_CompraDetalle_Material FOREIGN KEY (IdMaterial) REFERENCES VittoriaComprasDB_New.maestra.Material(IdMaterial);

ALTER TABLE VittoriaComprasDB_New.compras.CompraDocumento ADD CONSTRAINT FK_CompraDocumento_Compra FOREIGN KEY (IdCompra) REFERENCES VittoriaComprasDB_New.compras.Compra(IdCompra);

ALTER TABLE VittoriaComprasDB_New.compras.OrdenCompra ADD CONSTRAINT FK_OrdenCompra_Proveedor FOREIGN KEY (IdProveedor) REFERENCES VittoriaComprasDB_New.maestra.Proveedor(IdProveedor);
ALTER TABLE VittoriaComprasDB_New.compras.OrdenCompra ADD CONSTRAINT FK_OrdenCompra_Proyecto FOREIGN KEY (IdProyecto) REFERENCES VittoriaComprasDB_New.maestra.Proyecto(IdProyecto);
ALTER TABLE VittoriaComprasDB_New.compras.OrdenCompra ADD CONSTRAINT FK_OrdenCompra_Requerimiento FOREIGN KEY (IdRequerimiento) REFERENCES VittoriaComprasDB_New.compras.Requerimiento(IdRequerimiento);
ALTER TABLE VittoriaComprasDB_New.compras.OrdenCompra ADD CONSTRAINT FK_OrdenCompra_Usuario FOREIGN KEY (IdUsuarioCreacion) REFERENCES VittoriaComprasDB_New.seguridad.Usuario(IdUsuario);

ALTER TABLE VittoriaComprasDB_New.compras.OrdenCompraDetalle ADD CONSTRAINT FK_OrdenCompraDetalle_Material FOREIGN KEY (IdMaterial) REFERENCES VittoriaComprasDB_New.maestra.Material(IdMaterial);
ALTER TABLE VittoriaComprasDB_New.compras.OrdenCompraDetalle ADD CONSTRAINT FK_OrdenCompraDetalle_OrdenCompra FOREIGN KEY (IdOrdenCompra) REFERENCES VittoriaComprasDB_New.compras.OrdenCompra(IdOrdenCompra);
ALTER TABLE VittoriaComprasDB_New.compras.OrdenCompraDetalle ADD CONSTRAINT FK_OrdenCompraDetalle_Proveedor FOREIGN KEY (IdProveedor) REFERENCES VittoriaComprasDB_New.maestra.Proveedor(IdProveedor);

ALTER TABLE VittoriaComprasDB_New.compras.OrdenCompraHistorial ADD CONSTRAINT FK_OrdenCompraHistorial_OrdenCompra FOREIGN KEY (IdOrdenCompra) REFERENCES VittoriaComprasDB_New.compras.OrdenCompra(IdOrdenCompra);
ALTER TABLE VittoriaComprasDB_New.compras.OrdenCompraHistorial ADD CONSTRAINT FK_OrdenCompraHistorial_Usuario FOREIGN KEY (IdUsuario) REFERENCES VittoriaComprasDB_New.seguridad.Usuario(IdUsuario);

ALTER TABLE VittoriaComprasDB_New.compras.Requerimiento ADD CONSTRAINT FK_Requerimiento_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES VittoriaComprasDB_New.maestra.Especialidad(IdEspecialidad);
ALTER TABLE VittoriaComprasDB_New.compras.Requerimiento ADD CONSTRAINT FK_Requerimiento_Proyecto FOREIGN KEY (IdProyecto) REFERENCES VittoriaComprasDB_New.maestra.Proyecto(IdProyecto);
ALTER TABLE VittoriaComprasDB_New.compras.Requerimiento ADD CONSTRAINT FK_Requerimiento_Usuario FOREIGN KEY (IdUsuarioSolicitante) REFERENCES VittoriaComprasDB_New.seguridad.Usuario(IdUsuario);

ALTER TABLE VittoriaComprasDB_New.compras.RequerimientoDetalle ADD CONSTRAINT FK_RequerimientoDetalle_Material FOREIGN KEY (IdMaterial) REFERENCES VittoriaComprasDB_New.maestra.Material(IdMaterial);
ALTER TABLE VittoriaComprasDB_New.compras.RequerimientoDetalle ADD CONSTRAINT FK_RequerimientoDetalle_Requerimiento FOREIGN KEY (IdRequerimiento) REFERENCES VittoriaComprasDB_New.compras.Requerimiento(IdRequerimiento);

ALTER TABLE VittoriaComprasDB_New.compras.RequerimientoValidacion ADD CONSTRAINT FK_RequerimientoValidacion_Requerimiento FOREIGN KEY (IdRequerimiento) REFERENCES VittoriaComprasDB_New.compras.Requerimiento(IdRequerimiento);
ALTER TABLE VittoriaComprasDB_New.compras.RequerimientoValidacion ADD CONSTRAINT FK_RequerimientoValidacion_Usuario FOREIGN KEY (IdUsuario) REFERENCES VittoriaComprasDB_New.seguridad.Usuario(IdUsuario);


-- compras.vw_Compra_ListadoConEspecialidad source

/* ============================================================
   2. VIEW de compras con campos IGV
   ============================================================ */
ALTER VIEW [compras].[vw_Compra_ListadoConEspecialidad]
AS
SELECT
    c.IdCompra,
    c.NumeroCompra,
    c.FechaCompra,
    CASE WHEN c.Aceptada = 1 THEN 'Aceptada' ELSE 'Pendiente' END AS Estado,
    c.IncluyeIGV,
    c.SubtotalSinIGV,
    c.MontoIGV,
    c.MontoTotal,
    c.Observacion,
    p.RazonSocial AS Proveedor,
    e.Nombre AS Especialidad,
    pr.NombreProyecto,
    oc.NumeroOrdenCompra,
    r.NumeroRequerimiento
FROM compras.Compra c
INNER JOIN compras.OrdenCompra oc
    ON oc.IdOrdenCompra = c.IdOrdenCompra
LEFT JOIN compras.Requerimiento r
    ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proveedor p
    ON p.IdProveedor = c.IdProveedor
LEFT JOIN maestra.Especialidad e
    ON e.IdEspecialidad = r.IdEspecialidad
LEFT JOIN maestra.Proyecto pr
    ON pr.IdProyecto = COALESCE(oc.IdProyecto, r.IdProyecto);

