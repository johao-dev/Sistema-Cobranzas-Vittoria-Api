CREATE TYPE compras.TVP_CompraDetalle AS TABLE (
	IdMaterial int NOT NULL,
	Cantidad decimal(18, 2) NOT NULL,
	PrecioUnitario decimal(18, 2) NOT NULL
);

CREATE TYPE compras.TVP_CompraDocumento AS TABLE (
	TipoDocumento nvarchar(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	NumeroDocumento nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	RutaArchivo nvarchar(300) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaDocumento date NULL,
	Monto decimal(18, 2) NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
);

CREATE TYPE compras.TVP_OrdenCompraDetalle AS TABLE (
	IdMaterial int NOT NULL,
	Cantidad decimal(18, 2) NOT NULL,
	IdProveedor int NOT NULL,
	PrecioUnitario decimal(18, 2) NOT NULL
);

CREATE TYPE compras.TVP_RequerimientoDetalle AS TABLE (
	IdMaterial int NOT NULL,
	Cantidad decimal(18, 2) NOT NULL,
	Observacion nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
);
