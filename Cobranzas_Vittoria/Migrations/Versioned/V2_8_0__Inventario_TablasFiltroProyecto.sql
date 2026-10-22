/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.8.0
Módulo      : Almacen
Descripción : Agrega tablas de filtro para el módulo de inventario.
===============================================================================

Actualmente el módulo de inventario tiene filtro por especialidad y fechas, se
requiere agregar un filtro por proyecto.

La implementación de StockProyecto se realiza como una extensión incremental
del modelo actual de inventario, priorizando la entrega de la funcionalidad de
stock por proyecto con bajo impacto sobre los flujos existentes. A futuro se
contempla evolucionar el módulo hacia un modelo unificado de inventario que
permita gestionar explícitamente ubicaciones, almacenes, proyectos y 
movimientos mediante una estructura de stock y kardex más general.

===============================================================================
*/

/* ============================================================
   almacen.StockProyecto
   Distribución actual del stock global entre proyectos.
   ============================================================ */
CREATE TABLE almacen.StockProyecto (
    IdStockProyecto INT IDENTITY(1,1) NOT NULL,
    IdMaterial INT NOT NULL,
    IdProyecto INT NOT NULL,
    Stock DECIMAL(18,2) NOT NULL,
    FechaMovimiento DATETIME2(0) NOT NULL CONSTRAINT DF_StockProyecto_FechaUltimoMovimiento DEFAULT SYSDATETIME(),

    CONSTRAINT PK_StockProyecto
        PRIMARY KEY (IdStockProyecto),

    CONSTRAINT FK_StockProyecto_Material
        FOREIGN KEY (IdMaterial) REFERENCES maestra.Material(IdMaterial),

    CONSTRAINT FK_StockProyecto_Proyecto
        FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto),

    CONSTRAINT UQ_StockProyecto_Material_Proyecto
        UNIQUE (IdMaterial, IdProyecto),

    CONSTRAINT CK_StockProyecto_Stock_NoNegativo
        CHECK (Stock >= 0)
);
GO


/* ============================================================
   almacen.TransferenciaProyecto
   Cabecera de una reasignación de stock entre proyectos.
   No modifica el stock global.
   ============================================================ */
CREATE TABLE almacen.TransferenciaProyecto(
    IdTransferencia INT IDENTITY(1,1) NOT NULL,
    IdProyectoOrigen INT NOT NULL,
    IdProyectoDestino INT NOT NULL,
    Fecha DATE NOT NULL,
    Observacion NVARCHAR(500) NOT NULL,

    CONSTRAINT PK_TransferenciaProyecto
        PRIMARY KEY (IdTransferencia),

    CONSTRAINT FK_TransferenciaProyecto_ProyectoOrigen
        FOREIGN KEY (IdProyectoOrigen) REFERENCES maestra.Proyecto(IdProyecto),

    CONSTRAINT FK_TransferenciaProyecto_ProyectoDestino
        FOREIGN KEY (IdProyectoDestino) REFERENCES maestra.Proyecto(IdProyecto),

    CONSTRAINT CK_TransferenciaProyecto_ProyectosDistintos
        CHECK (IdProyectoOrigen <> IdProyectoDestino)
);
GO


/* ============================================================
   almacen.TransferenciaProyectoDetalle
   Materiales y cantidades trasladadas entre proyectos.
   ============================================================ */
CREATE TABLE almacen.TransferenciaProyectoDetalle(
    IdTransferenciaProyectoDetalle INT IDENTITY(1,1) NOT NULL,
    IdTransferencia INT NOT NULL,
    IdMaterial INT NOT NULL,
    Cantidad DECIMAL(18,2) NOT NULL,

    CONSTRAINT PK_TransferenciaProyectoDetalle
        PRIMARY KEY (IdTransferenciaProyectoDetalle),

    CONSTRAINT FK_TransferenciaProyectoDetalle_Transferencia
        FOREIGN KEY (IdTransferencia) REFERENCES almacen.TransferenciaProyecto(IdTransferencia),

    CONSTRAINT FK_TransferenciaProyectoDetalle_Material
        FOREIGN KEY (IdMaterial) REFERENCES maestra.Material(IdMaterial),

    CONSTRAINT CK_TransferenciaProyectoDetalle_CantidadPositiva
        CHECK (Cantidad > 0),

    CONSTRAINT UQ_TransferenciaProyectoDetalle_Transferencia_Material
        UNIQUE (IdTransferencia, IdMaterial)
);
GO

/* Indices para consultas habituales */
CREATE INDEX IX_StockProyecto_Proyecto ON almacen.StockProyecto(IdProyecto, IdMaterial);
CREATE INDEX IX_TransferenciaProyecto_Fecha ON almacen.TransferenciaProyecto(Fecha);
GO