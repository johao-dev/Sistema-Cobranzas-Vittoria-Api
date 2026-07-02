CREATE TABLE VittoriaComprasDB_New.almacen.KardexMovimiento (
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
ALTER TABLE VittoriaComprasDB_New.almacen.KardexMovimiento WITH NOCHECK ADD CONSTRAINT CK_KardexMovimiento_Tipo CHECK (([TipoMovimiento]=N'AJUSTE' OR [TipoMovimiento]=N'SALIDA' OR [TipoMovimiento]=N'ENTRADA' OR [TipoMovimiento]=N'INVENTARIO_INICIAL'));

ALTER TABLE VittoriaComprasDB_New.almacen.KardexMovimiento ADD CONSTRAINT FK_KardexMovimiento_Compra FOREIGN KEY (IdCompra) REFERENCES VittoriaComprasDB_New.compras.Compra(IdCompra);
ALTER TABLE VittoriaComprasDB_New.almacen.KardexMovimiento ADD CONSTRAINT FK_KardexMovimiento_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES VittoriaComprasDB_New.maestra.Especialidad(IdEspecialidad);
ALTER TABLE VittoriaComprasDB_New.almacen.KardexMovimiento ADD CONSTRAINT FK_KardexMovimiento_Material FOREIGN KEY (IdMaterial) REFERENCES VittoriaComprasDB_New.maestra.Material(IdMaterial);
ALTER TABLE VittoriaComprasDB_New.almacen.KardexMovimiento ADD CONSTRAINT FK_KardexMovimiento_OrdenCompra FOREIGN KEY (IdOrdenCompra) REFERENCES VittoriaComprasDB_New.compras.OrdenCompra(IdOrdenCompra);

ALTER   VIEW [almacen].[vw_Kardex_DesdeComprasYMovimientos]
AS
WITH BaseCompras AS
(
    SELECT
        km.IdKardexMovimiento,
        km.IdMaterial,
        km.IdEspecialidad,
        km.TipoMovimiento,
        km.FechaMovimiento,
        km.CantidadEntrada,
        km.CantidadSalida,
        km.StockResultante,
        km.IdCompra,
        km.IdOrdenCompra,
        km.Observacion,
        km.FechaIngresoAlmacen,
        km.FechaSalidaAlmacen,
        km.FechaCreacion
    FROM almacen.KardexMovimiento km
),
Enriquecido AS
(
    SELECT
        b.IdKardexMovimiento,
        b.IdMaterial,
        m.Descripcion AS Material,
        COALESCE(b.IdEspecialidad, m.IdEspecialidad) AS IdEspecialidad,
        e.Nombre AS Especialidad,
        b.TipoMovimiento,
        b.FechaMovimiento,
        CAST(ISNULL(b.CantidadEntrada, 0) AS DECIMAL(18,2)) AS Entrada,
        CAST(ISNULL(b.CantidadSalida, 0) AS DECIMAL(18,2)) AS Salida,
        CAST(ISNULL(b.StockResultante, 0) AS DECIMAL(18,2)) AS Stock,
        b.IdCompra,
        c.NumeroCompra,
        b.IdOrdenCompra,
        oc.NumeroOrdenCompra,
        ISNULL(b.Observacion, '') AS Observacion,
        b.FechaIngresoAlmacen,
        b.FechaSalidaAlmacen,
        b.FechaCreacion
    FROM BaseCompras b
    LEFT JOIN maestra.Material m ON m.IdMaterial = b.IdMaterial
    LEFT JOIN maestra.Especialidad e ON e.IdEspecialidad = COALESCE(b.IdEspecialidad, m.IdEspecialidad)
    LEFT JOIN compras.Compra c ON c.IdCompra = b.IdCompra
    LEFT JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = b.IdOrdenCompra
)
SELECT *
FROM Enriquecido;

ALTER   VIEW [almacen].[vw_Kardex_PorEspecialidad]
AS
SELECT
    km.IdKardexMovimiento,
    km.IdEspecialidad,
    e.Nombre AS Especialidad,
    km.IdMaterial,
    m.Descripcion AS Material,
    m.UnidadMedida,
    km.TipoMovimiento,
    km.FechaMovimiento,
    km.CantidadEntrada,
    km.CantidadSalida,
    km.StockResultante,
    km.IdCompra,
    km.IdOrdenCompra,
    km.Observacion,
    km.FechaIngresoAlmacen,
    km.FechaSalidaAlmacen,
    km.FechaCreacion
FROM almacen.KardexMovimiento km
INNER JOIN maestra.Material m
    ON m.IdMaterial = km.IdMaterial
LEFT JOIN maestra.Especialidad e
    ON e.IdEspecialidad = km.IdEspecialidad;
