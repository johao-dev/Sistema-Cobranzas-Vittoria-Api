CREATE TYPE TVP_PresupuestoProyectoDetalle AS TABLE (
	Orden int NOT NULL,
	Concepto nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Soles decimal(18, 2) NOT NULL,
	Incidencia decimal(9, 2) NULL
);
