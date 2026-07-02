CREATE TABLE VittoriaComprasDB_New.contable.CotizacionMaterialEspecialidad (
	IdCotizacionMaterialEspecialidad int IDENTITY(1,1) NOT NULL,
	IdProyecto int NOT NULL,
	IdEspecialidad int NOT NULL,
	Cotizacion decimal(18,2) DEFAULT 0 NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime DEFAULT getdate() NOT NULL,
	FechaActualizacion datetime NULL,
	CONSTRAINT PK_CotizacionMaterialEspecialidad PRIMARY KEY (IdCotizacionMaterialEspecialidad)
);
 CREATE NONCLUSTERED INDEX IX_CotizacionMaterialEspecialidad_Proyecto_Especialidad ON VittoriaComprasDB_New.contable.CotizacionMaterialEspecialidad (  IdProyecto ASC  , IdEspecialidad ASC  , Activo ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;
 CREATE UNIQUE NONCLUSTERED INDEX UX_CotizacionMaterialEspecialidad_ProyectoEspecialidad ON VittoriaComprasDB_New.contable.CotizacionMaterialEspecialidad (  IdProyecto ASC  , IdEspecialidad ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;
 CREATE UNIQUE NONCLUSTERED INDEX UX_CotizacionMaterialEspecialidad_Proyecto_Especialidad_Activo ON VittoriaComprasDB_New.contable.CotizacionMaterialEspecialidad (  IdProyecto ASC  , IdEspecialidad ASC  )  
	 WHERE  ([Activo]=(1) AND [IdProyecto] IS NOT NULL)
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE VittoriaComprasDB_New.contable.GastoAdministrativo (
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
 CREATE NONCLUSTERED INDEX IX_GastoAdministrativo_IdProyecto ON VittoriaComprasDB_New.contable.GastoAdministrativo (  IdProyecto ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE VittoriaComprasDB_New.contable.GastoAdministrativoDocumento (
	IdGastoAdministrativoDocumento int IDENTITY(1,1) NOT NULL,
	IdGastoAdministrativo int NOT NULL,
	TipoDocumento nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	NombreArchivo nvarchar(255) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	RutaArchivo nvarchar(400) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Extension nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion datetime2(0) DEFAULT getdate() NOT NULL,
	CONSTRAINT PK__GastoAdm__41AD2D1BD512C312 PRIMARY KEY (IdGastoAdministrativoDocumento)
);
ALTER TABLE VittoriaComprasDB_New.contable.GastoAdministrativoDocumento WITH NOCHECK ADD CONSTRAINT CK_GastoAdministrativoDocumento_TipoDocumento CHECK (([TipoDocumento]='Pago' OR [TipoDocumento]='Factura'));

CREATE TABLE VittoriaComprasDB_New.contable.GastoProyecto (
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
 CREATE NONCLUSTERED INDEX IX_GastoProyecto_ModuloProyecto ON VittoriaComprasDB_New.contable.GastoProyecto (  TipoModulo ASC  , IdProyecto ASC  , Estado ASC  , Activo ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;
ALTER TABLE VittoriaComprasDB_New.contable.GastoProyecto WITH NOCHECK ADD CONSTRAINT CK_GastoProyecto_TipoModulo CHECK (([TipoModulo]='GastosMunicipales' OR [TipoModulo]='OtrosGastos' OR [TipoModulo]='Marketing' OR [TipoModulo]='Terreno'));
ALTER TABLE VittoriaComprasDB_New.contable.GastoProyecto WITH NOCHECK ADD CONSTRAINT CK_GastoProyecto_Estado CHECK (([Estado]='Inactivo' OR [Estado]='Activo'));

CREATE TABLE VittoriaComprasDB_New.contable.GastoProyectoDocumento (
	IdGastoProyectoDocumento int IDENTITY(1,1) NOT NULL,
	IdGastoProyecto int NOT NULL,
	TipoDocumento nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS DEFAULT 'Factura' NOT NULL,
	NombreArchivo nvarchar(255) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	RutaArchivo nvarchar(500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Extension nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion datetime DEFAULT getdate() NOT NULL,
	CONSTRAINT PK_GastoProyectoDocumento PRIMARY KEY (IdGastoProyectoDocumento)
);
 CREATE NONCLUSTERED INDEX IX_GastoProyectoDocumento_Gasto ON VittoriaComprasDB_New.contable.GastoProyectoDocumento (  IdGastoProyecto ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE VittoriaComprasDB_New.contable.PresupuestoProyecto (
	IdPresupuestoProyecto int IDENTITY(1,1) NOT NULL,
	IdProyecto int NOT NULL,
	Activo bit DEFAULT 1 NOT NULL,
	FechaCreacion datetime2(0) DEFAULT sysdatetime() NOT NULL,
	FechaActualizacion datetime2(0) NULL,
	CONSTRAINT PK_PresupuestoProyecto PRIMARY KEY (IdPresupuestoProyecto),
	CONSTRAINT UQ_PresupuestoProyecto_IdProyecto UNIQUE (IdProyecto)
);
 CREATE UNIQUE NONCLUSTERED INDEX UX_PresupuestoProyecto_IdProyecto ON VittoriaComprasDB_New.contable.PresupuestoProyecto (  IdProyecto ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE VittoriaComprasDB_New.contable.PresupuestoProyectoDetalle (
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
 CREATE NONCLUSTERED INDEX IX_PresupuestoProyectoDetalle_IdPresupuestoProyecto ON VittoriaComprasDB_New.contable.PresupuestoProyectoDetalle (  IdPresupuestoProyecto ASC  , Orden ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE VittoriaComprasDB_New.contable.Terreno (
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
 CREATE NONCLUSTERED INDEX IX_Terreno_IdProyecto ON VittoriaComprasDB_New.contable.Terreno (  IdProyecto ASC  )  
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE VittoriaComprasDB_New.contable.Valorizacion (
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

CREATE TABLE VittoriaComprasDB_New.contable.ValorizacionDetalle (
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
 CREATE NONCLUSTERED INDEX IX_ValorizacionDetalle_IdProveedorEspecialidadCotizacion ON VittoriaComprasDB_New.contable.ValorizacionDetalle (  IdProveedorEspecialidadCotizacion ASC  )  
	 INCLUDE ( Activo , FechaFactura , IdValorizacion , IdValorizacionDetalle ) 
	 WITH (  PAD_INDEX = OFF ,FILLFACTOR = 100  ,SORT_IN_TEMPDB = OFF , IGNORE_DUP_KEY = OFF , STATISTICS_NORECOMPUTE = OFF , ONLINE = OFF , ALLOW_ROW_LOCKS = ON , ALLOW_PAGE_LOCKS = ON  )
	 ON [PRIMARY ] ;

CREATE TABLE VittoriaComprasDB_New.contable.ValorizacionDetalleArchivo (
	IdValorizacionDetalleArchivo int IDENTITY(1,1) NOT NULL,
	IdValorizacionDetalle int NOT NULL,
	NombreArchivo nvarchar(260) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	RutaArchivo nvarchar(500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Extension nvarchar(20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion datetime DEFAULT getdate() NOT NULL,
	CONSTRAINT PK__Valoriza__3A054B96D0D79D32 PRIMARY KEY (IdValorizacionDetalleArchivo)
);

ALTER TABLE VittoriaComprasDB_New.contable.CotizacionMaterialEspecialidad ADD CONSTRAINT FK_CotizacionMaterialEspecialidad_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES VittoriaComprasDB_New.maestra.Especialidad(IdEspecialidad);
ALTER TABLE VittoriaComprasDB_New.contable.CotizacionMaterialEspecialidad ADD CONSTRAINT FK_CotizacionMaterialEspecialidad_Proyecto FOREIGN KEY (IdProyecto) REFERENCES VittoriaComprasDB_New.maestra.Proyecto(IdProyecto);

ALTER TABLE VittoriaComprasDB_New.contable.GastoAdministrativo ADD CONSTRAINT FK_GastoAdministrativo_CategoriaGasto FOREIGN KEY (IdCategoriaGasto) REFERENCES VittoriaComprasDB_New.maestra.CategoriaGasto(IdCategoriaGasto);
ALTER TABLE VittoriaComprasDB_New.contable.GastoAdministrativo ADD CONSTRAINT FK_GastoAdministrativo_Proveedor FOREIGN KEY (IdProveedor) REFERENCES VittoriaComprasDB_New.maestra.Proveedor(IdProveedor);
ALTER TABLE VittoriaComprasDB_New.contable.GastoAdministrativo ADD CONSTRAINT FK_GastoAdministrativo_ProveedorGastoAdministrativo FOREIGN KEY (IdProveedorGastoAdministrativo) REFERENCES VittoriaComprasDB_New.maestra.ProveedorGastoAdministrativo(IdProveedorGastoAdministrativo);
ALTER TABLE VittoriaComprasDB_New.contable.GastoAdministrativo ADD CONSTRAINT FK_GastoAdministrativo_Proyecto FOREIGN KEY (IdProyecto) REFERENCES VittoriaComprasDB_New.maestra.Proyecto(IdProyecto);

ALTER TABLE VittoriaComprasDB_New.contable.GastoAdministrativoDocumento ADD CONSTRAINT FK_GastoAdministrativoDocumento_GastoAdministrativo FOREIGN KEY (IdGastoAdministrativo) REFERENCES VittoriaComprasDB_New.contable.GastoAdministrativo(IdGastoAdministrativo);

ALTER TABLE VittoriaComprasDB_New.contable.GastoProyecto ADD CONSTRAINT FK_GastoProyecto_ProveedorTerreno FOREIGN KEY (IdProveedorTerreno) REFERENCES VittoriaComprasDB_New.maestra.ProveedorTerreno(IdProveedorTerreno);
ALTER TABLE VittoriaComprasDB_New.contable.GastoProyecto ADD CONSTRAINT FK_GastoProyecto_Proyecto FOREIGN KEY (IdProyecto) REFERENCES VittoriaComprasDB_New.maestra.Proyecto(IdProyecto);

ALTER TABLE VittoriaComprasDB_New.contable.GastoProyectoDocumento ADD CONSTRAINT FK_GastoProyectoDocumento_GastoProyecto FOREIGN KEY (IdGastoProyecto) REFERENCES VittoriaComprasDB_New.contable.GastoProyecto(IdGastoProyecto);

ALTER TABLE VittoriaComprasDB_New.contable.PresupuestoProyecto ADD CONSTRAINT FK_PresupuestoProyecto_Proyecto FOREIGN KEY (IdProyecto) REFERENCES VittoriaComprasDB_New.maestra.Proyecto(IdProyecto);

ALTER TABLE VittoriaComprasDB_New.contable.PresupuestoProyectoDetalle ADD CONSTRAINT FK_PresupuestoProyectoDetalle_PresupuestoProyecto FOREIGN KEY (IdPresupuestoProyecto) REFERENCES VittoriaComprasDB_New.contable.PresupuestoProyecto(IdPresupuestoProyecto);

ALTER TABLE VittoriaComprasDB_New.contable.Terreno ADD CONSTRAINT FK_Terreno_Proyecto FOREIGN KEY (IdProyecto) REFERENCES VittoriaComprasDB_New.maestra.Proyecto(IdProyecto);

ALTER TABLE VittoriaComprasDB_New.contable.Valorizacion ADD CONSTRAINT FK_Valorizacion_Especialidad FOREIGN KEY (IdEspecialidad) REFERENCES VittoriaComprasDB_New.maestra.Especialidad(IdEspecialidad);
ALTER TABLE VittoriaComprasDB_New.contable.Valorizacion ADD CONSTRAINT FK_Valorizacion_PECotizacion FOREIGN KEY (IdProveedorEspecialidadCotizacion) REFERENCES VittoriaComprasDB_New.maestra.ProveedorEspecialidadCotizacion(IdProveedorEspecialidadCotizacion);
ALTER TABLE VittoriaComprasDB_New.contable.Valorizacion ADD CONSTRAINT FK_Valorizacion_Proveedor FOREIGN KEY (IdProveedor) REFERENCES VittoriaComprasDB_New.maestra.Proveedor(IdProveedor);
ALTER TABLE VittoriaComprasDB_New.contable.Valorizacion ADD CONSTRAINT FK_Valorizacion_Proyecto FOREIGN KEY (IdProyecto) REFERENCES VittoriaComprasDB_New.maestra.Proyecto(IdProyecto);

ALTER TABLE VittoriaComprasDB_New.contable.ValorizacionDetalle ADD CONSTRAINT FK_ValDet_Valorizacion FOREIGN KEY (IdValorizacion) REFERENCES VittoriaComprasDB_New.contable.Valorizacion(IdValorizacion);

ALTER TABLE VittoriaComprasDB_New.contable.ValorizacionDetalleArchivo ADD CONSTRAINT FK_ValorizacionDetalleArchivo_Detalle FOREIGN KEY (IdValorizacionDetalle) REFERENCES VittoriaComprasDB_New.contable.ValorizacionDetalle(IdValorizacionDetalle);


ALTER VIEW contable.vw_CotizacionMaterialesPorProyecto
AS
SELECT
    p.IdProyecto,
    p.NombreProyecto AS Proyecto,
    SUM(ISNULL(c.Cotizacion, 0)) AS CotizacionMateriales
FROM maestra.Proyecto p
LEFT JOIN contable.CotizacionMaterialEspecialidad c
    ON c.IdProyecto = p.IdProyecto
   AND ISNULL(c.Activo, 1) = 1
WHERE ISNULL(p.Activo, 1) = 1
GROUP BY p.IdProyecto, p.NombreProyecto;

ALTER VIEW contable.vw_CotizacionMaterialesResumenTodosProyectos
AS
WITH Cotizaciones AS
(
    SELECT
        c.IdProyecto,
        p.NombreProyecto AS Proyecto,
        c.IdEspecialidad,
        e.Nombre AS Especialidad,
        UPPER(LTRIM(RTRIM(e.Nombre))) COLLATE Latin1_General_CI_AI AS EspecialidadKey,
        SUM(ISNULL(c.Cotizacion, 0)) AS Cotizacion
    FROM contable.CotizacionMaterialEspecialidad c
    INNER JOIN maestra.Proyecto p
        ON p.IdProyecto = c.IdProyecto
    INNER JOIN maestra.Especialidad e
        ON e.IdEspecialidad = c.IdEspecialidad
    WHERE ISNULL(c.Activo, 1) = 1
      AND ISNULL(p.Activo, 1) = 1
      AND ISNULL(e.Activo, 1) = 1
    GROUP BY c.IdProyecto, p.NombreProyecto, c.IdEspecialidad, e.Nombre
),
Facturado AS
(
    SELECT
        COALESCE(oc.IdProyecto, r.IdProyecto) AS IdProyecto,
        p.NombreProyecto AS Proyecto,
        m.IdEspecialidad,
        e.Nombre AS Especialidad,
        UPPER(LTRIM(RTRIM(e.Nombre))) COLLATE Latin1_General_CI_AI AS EspecialidadKey,
        SUM(
            ISNULL(
                TRY_CONVERT(DECIMAL(18,2), d.Subtotal),
                ISNULL(TRY_CONVERT(DECIMAL(18,2), d.Cantidad), 0) * ISNULL(TRY_CONVERT(DECIMAL(18,2), d.PrecioUnitario), 0)
            )
        ) AS Facturado
    FROM compras.Compra c
    INNER JOIN compras.CompraDetalle d
        ON d.IdCompra = c.IdCompra
    INNER JOIN maestra.Material m
        ON m.IdMaterial = d.IdMaterial
    INNER JOIN maestra.Especialidad e
        ON e.IdEspecialidad = m.IdEspecialidad
    INNER JOIN compras.OrdenCompra oc
        ON oc.IdOrdenCompra = c.IdOrdenCompra
    LEFT JOIN compras.Requerimiento r
        ON r.IdRequerimiento = oc.IdRequerimiento
    INNER JOIN maestra.Proyecto p
        ON p.IdProyecto = COALESCE(oc.IdProyecto, r.IdProyecto)
    WHERE COALESCE(oc.IdProyecto, r.IdProyecto) IS NOT NULL
    GROUP BY COALESCE(oc.IdProyecto, r.IdProyecto), p.NombreProyecto, m.IdEspecialidad, e.Nombre
),
FacturadoConCotizacion AS
(
    SELECT
        f.IdProyecto,
        f.Proyecto,
        f.IdEspecialidad,
        f.Especialidad,
        ISNULL(ca.CotizacionAplicable, 0) AS Cotizacion,
        ISNULL(f.Facturado, 0) AS Facturado
    FROM Facturado f
    OUTER APPLY
    (
        SELECT SUM(c.Cotizacion) AS CotizacionAplicable
        FROM Cotizaciones c
        WHERE c.IdProyecto = f.IdProyecto
          AND
          (
                c.IdEspecialidad = f.IdEspecialidad
             OR c.EspecialidadKey = f.EspecialidadKey
             OR EXISTS
                (
                    SELECT 1
                    FROM STRING_SPLIT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(f.Especialidad, ';', ','), '/', ','), '|', ','), '+', ','), '&', ','), ',') s
                    WHERE UPPER(LTRIM(RTRIM(s.value))) COLLATE Latin1_General_CI_AI = c.EspecialidadKey
                )
          )
    ) ca
),
CotizacionesSinFacturado AS
(
    SELECT
        c.IdProyecto,
        c.Proyecto,
        c.IdEspecialidad,
        c.Especialidad,
        c.Cotizacion,
        CAST(0 AS DECIMAL(18,2)) AS Facturado
    FROM Cotizaciones c
    WHERE ISNULL(c.Cotizacion, 0) <> 0
      AND NOT EXISTS
      (
          SELECT 1
          FROM Facturado f
          WHERE f.IdProyecto = c.IdProyecto
            AND
            (
                f.IdEspecialidad = c.IdEspecialidad
             OR f.EspecialidadKey = c.EspecialidadKey
             OR EXISTS
                (
                    SELECT 1
                    FROM STRING_SPLIT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(f.Especialidad, ';', ','), '/', ','), '|', ','), '+', ','), '&', ','), ',') s
                    WHERE UPPER(LTRIM(RTRIM(s.value))) COLLATE Latin1_General_CI_AI = c.EspecialidadKey
                )
            )
      )
)
SELECT
    IdProyecto,
    Proyecto,
    IdEspecialidad,
    Especialidad,
    Cotizacion,
    Facturado,
    Cotizacion - Facturado AS Saldo
FROM FacturadoConCotizacion
UNION ALL
SELECT
    IdProyecto,
    Proyecto,
    IdEspecialidad,
    Especialidad,
    Cotizacion,
    Facturado,
    Cotizacion - Facturado AS Saldo
FROM CotizacionesSinFacturado;

ALTER VIEW [contable].[vw_PresupuestoProyectoResumen]
AS
WITH Presupuesto AS
(
    SELECT
        pp.IdPresupuestoProyecto,
        pp.IdProyecto,
        p.NombreProyecto,
        CAST(ISNULL(SUM(CASE WHEN ISNULL(ppd.Activo,1) = 1 THEN ISNULL(ppd.Soles,0) ELSE 0 END), 0) AS DECIMAL(18,2)) AS TotalPresupuesto
    FROM [contable].[PresupuestoProyecto] pp
    INNER JOIN [maestra].[Proyecto] p
        ON p.IdProyecto = pp.IdProyecto
    LEFT JOIN [contable].[PresupuestoProyectoDetalle] ppd
        ON ppd.IdPresupuestoProyecto = pp.IdPresupuestoProyecto
    WHERE ISNULL(pp.Activo,1) = 1
    GROUP BY
        pp.IdPresupuestoProyecto,
        pp.IdProyecto,
        p.NombreProyecto
),
ComprasProyecto AS
(
    SELECT
        oc.IdProyecto,
        CAST(ISNULL(SUM(ISNULL(c.MontoTotal, 0)), 0) AS DECIMAL(18,2)) AS TotalCompras
    FROM [compras].[OrdenCompra] oc
    LEFT JOIN [compras].[Compra] c
        ON c.IdOrdenCompra = oc.IdOrdenCompra
    GROUP BY
        oc.IdProyecto
)
SELECT
    pr.IdPresupuestoProyecto,
    pr.IdProyecto,
    pr.NombreProyecto,
    pr.TotalPresupuesto,
    CAST(ISNULL(cp.TotalCompras, 0) AS DECIMAL(18,2)) AS TotalCompras,
    CAST(pr.TotalPresupuesto - ISNULL(cp.TotalCompras, 0) AS DECIMAL(18,2)) AS SaldoRestante
FROM Presupuesto pr
LEFT JOIN ComprasProyecto cp
    ON cp.IdProyecto = pr.IdProyecto;

ALTER VIEW [contable].[vw_ValorizacionDetalleCalculado]
AS
WITH Base AS
(
    SELECT
        v.IdValorizacion,
        v.NumeroValorizacion,
        v.IdProyecto,
        p.NombreProyecto,
        v.IdProveedor,
        pr.RazonSocial AS Proveedor,
        v.IdEspecialidad,
        e.Nombre AS Especialidad,
        v.Empresa,
        v.Servicio,
        v.Moneda,
        v.Cotizacion,
        v.PorcentajeGarantia AS PorcentajeGarantiaCabecera,
        v.PorcentajeDetraccion AS PorcentajeDetraccionCabecera,
        d.IdValorizacionDetalle,
        d.FechaFactura,
        d.NumeroFactura,
        d.BaseImponible,
        d.Igv,
        d.MontoFactura,
        d.Descripcion,
        d.MontoDetraccion,
        d.MontoGarantia,
        d.OtrosDescuentos,
        d.MontoAbonar,
        d.FechaTransferencia,
        d.NumeroOperacion,
        d.BancoTransferencia,
        d.BancoDestino,
        d.MontoTransferido,
        d.MontoAFavor,
        d.MontoDeuda,
        d.Activo
    FROM contable.Valorizacion v
    INNER JOIN contable.ValorizacionDetalle d
        ON d.IdValorizacion = v.IdValorizacion
    INNER JOIN maestra.Proveedor pr
        ON pr.IdProveedor = v.IdProveedor
    INNER JOIN maestra.Especialidad e
        ON e.IdEspecialidad = v.IdEspecialidad
    LEFT JOIN maestra.Proyecto p
        ON p.IdProyecto = v.IdProyecto
    WHERE v.Activo = 1
      AND d.Activo = 1
)
SELECT
    b.*,
    CAST(ROUND(CASE WHEN b.Cotizacion > 0 THEN b.MontoFactura / b.Cotizacion ELSE 0 END, 6) AS DECIMAL(18,6)) AS PorcentajeAvance,
    CAST(ROUND(
        SUM(CASE WHEN b.Cotizacion > 0 THEN b.MontoFactura / b.Cotizacion ELSE 0 END)
        OVER (PARTITION BY b.IdValorizacion ORDER BY ISNULL(b.FechaFactura, '19000101'), b.IdValorizacionDetalle ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
    , 6) AS DECIMAL(18,6)) AS PorcentajeAcumulado,
    CAST(ROUND(
        ISNULL(
            SUM(CASE WHEN b.Cotizacion > 0 THEN b.MontoFactura / b.Cotizacion ELSE 0 END)
            OVER (PARTITION BY b.IdValorizacion ORDER BY ISNULL(b.FechaFactura, '19000101'), b.IdValorizacionDetalle ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
        , 0)
    , 6) AS DECIMAL(18,6)) AS PorcentajeInicial,
    CAST(ROUND(
        ISNULL(
            SUM(CASE WHEN b.Cotizacion > 0 THEN b.MontoFactura / b.Cotizacion ELSE 0 END)
            OVER (PARTITION BY b.IdValorizacion ORDER BY ISNULL(b.FechaFactura, '19000101'), b.IdValorizacionDetalle ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
        , 0)
        + CASE WHEN b.Cotizacion > 0 THEN b.MontoFactura / b.Cotizacion ELSE 0 END
    , 6) AS DECIMAL(18,6)) AS PorcentajeFinal
FROM Base b;


ALTER VIEW [contable].[vw_ValorizacionResumen]
AS
SELECT
    v.IdValorizacion,
    v.NumeroValorizacion,
    v.IdProyecto,
    p.NombreProyecto,
    v.IdProveedor,
    pr.RazonSocial AS Proveedor,
    v.IdEspecialidad,
    e.Nombre AS Especialidad,
    v.Empresa,
    v.Servicio,
    v.Moneda,
    v.Cotizacion,
    v.PorcentajeGarantia,
    v.PorcentajeDetraccion,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoFactura END), 0) AS DECIMAL(18,2)) AS Facturado,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoTransferido END), 0) AS DECIMAL(18,2)) AS Transferido,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoGarantia END), 0) AS DECIMAL(18,2)) AS GarantiaRetenida,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoDetraccion END), 0) AS DECIMAL(18,2)) AS DetraccionAcumulada,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.OtrosDescuentos END), 0) AS DECIMAL(18,2)) AS OtrosDescuentos,
    CAST(v.Cotizacion - ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoFactura END), 0) AS DECIMAL(18,2)) AS Resta,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoGarantia END), 0) + (v.Cotizacion - ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoFactura END), 0)) AS DECIMAL(18,2)) AS Liquidar,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoAFavor END), 0) AS DECIMAL(18,2)) AS AFavor,
    CAST(ISNULL(SUM(CASE WHEN d.Activo = 1 THEN d.MontoDeuda END), 0) AS DECIMAL(18,2)) AS Deuda,
    v.Activo,
    v.FechaCreacion
FROM contable.Valorizacion v
INNER JOIN maestra.Proveedor pr
    ON pr.IdProveedor = v.IdProveedor
INNER JOIN maestra.Especialidad e
    ON e.IdEspecialidad = v.IdEspecialidad
LEFT JOIN maestra.Proyecto p
    ON p.IdProyecto = v.IdProyecto
LEFT JOIN contable.ValorizacionDetalle d
    ON d.IdValorizacion = v.IdValorizacion
WHERE v.Activo = 1
GROUP BY
    v.IdValorizacion,
    v.NumeroValorizacion,
    v.IdProyecto,
    p.NombreProyecto,
    v.IdProveedor,
    pr.RazonSocial,
    v.IdEspecialidad,
    e.Nombre,
    v.Empresa,
    v.Servicio,
    v.Moneda,
    v.Cotizacion,
    v.PorcentajeGarantia,
    v.PorcentajeDetraccion,
    v.Activo,
    v.FechaCreacion;
