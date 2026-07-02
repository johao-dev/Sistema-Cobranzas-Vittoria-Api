CREATE TABLE almacen.KardexMovimiento (
	IdKardexMovimiento int IDENTITY(1,1) NOT NULL,
	IdMaterial int NOT NULL,
	IdEspecialidad int NOT NULL,
	TipoMovimiento nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	FechaMovimiento date NOT NULL,
	CantidadEntrada decimal(18,2) DEFAULT 0 NOT NULL,
	CantidadSalida decimal(18,2) DEFAULT 0 NOT NULL,
	StockResultante decimal(18,2) DEFAULT 0 NOT NULL,
	IdCompra int NULL,
	IdOrdenCompra int NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaIngresoAlmacen date NULL,
	FechaSalidaAlmacen date NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK__KardexMo__F53E14894CAB3687 PRIMARY KEY (IdKardexMovimiento)
);
ALTER TABLE almacen.KardexMovimiento WITH NOCHECK ADD CONSTRAINT CK_KardexMovimiento_Tipo CHECK (([TipoMovimiento]=N'AJUSTE' OR [TipoMovimiento]=N'SALIDA' OR [TipoMovimiento]=N'ENTRADA' OR [TipoMovimiento]=N'INVENTARIO_INICIAL'));

ALTER TABLE almacen.KardexMovimiento ADD CONSTRAINT FK_KardexMovimiento_Compra FOREIGN KEY (IdCompra) REFERENCES compras.Compra(IdCompra);
ALTER TABLE almacen.KardexMovimiento ADD CONSTRAINT FK_KardexMovimiento_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES maestra.Especialidad(IdEspecialidad);
ALTER TABLE almacen.KardexMovimiento ADD CONSTRAINT FK_KardexMovimiento_Material FOREIGN KEY (IdMaterial) REFERENCES maestra.Material(IdMaterial);
ALTER TABLE almacen.KardexMovimiento ADD CONSTRAINT FK_KardexMovimiento_OrdenCompra FOREIGN KEY (IdOrdenCompra) REFERENCES compras.OrdenCompra(IdOrdenCompra);
