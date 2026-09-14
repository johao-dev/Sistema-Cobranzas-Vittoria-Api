/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.0.6
Módulo      : Control Presupuestario
Descripción : Creación de la entidad PresupuestoDetalle.
Autor       : Johao Bravo
===============================================================================
*/


/*
===============================================================================
PRESUPUESTO DETALLE
-------------------------------------------------------------------------------
Representa la asignación presupuestaria de una partida dentro de una versión
específica de un presupuesto.

Ejemplo:

    Presupuesto Talara 702
        │
        └── V1
            ├── Concreto      S/ 2,500,000
            ├── Acero         S/ 1,800,000
            └── Excavación    S/   700,000

Cada PresupuestoVersion contiene su propio conjunto de PresupuestoDetalle.

Esto permite conservar snapshots completos e independientes:

    V1
        Concreto   S/ 2,500,000

    V2
        Concreto   S/ 2,800,000

Modificar V2 no altera los registros correspondientes a V1.

REGLAS ESTRUCTURALES:

    - Cada detalle pertenece exactamente a una versión.
    - Cada detalle referencia exactamente una partida del catálogo.
    - Una partida no puede aparecer dos veces dentro de la misma versión.
    - El monto presupuestado no puede ser negativo.

REGLAS DE NEGOCIO DE LA API:

    - Solo pueden modificarse detalles de versiones BORRADOR.
    - Una versión APROBADA/HISTORICO/ANULADA es inmutable.
    - Solo las partidas hoja deben recibir montos presupuestados.
    - Una partida inactiva no puede agregarse a nuevas versiones.
===============================================================================
*/
CREATE TABLE ControlPresupuestario.PresupuestoDetalle
(
    IdPresupuestoDetalle INT IDENTITY(1,1) NOT NULL,
    IdPresupuestoVersion INT NOT NULL,
    IdCatalogoPartida INT NOT NULL,
    MontoPresupuestado DECIMAL(18,2) NOT NULL,
    Observacion NVARCHAR(500) NULL,
    FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_PresupuestoDetalle_FechaCreacion DEFAULT (SYSDATETIME()),
    FechaModificacion DATETIME2(0) NULL,

    CONSTRAINT PK_PresupuestoDetalle PRIMARY KEY CLUSTERED(IdPresupuestoDetalle),
    CONSTRAINT FK_PresupuestoDetalle_PresupuestoVersion FOREIGN KEY(IdPresupuestoVersion)
        REFERENCES ControlPresupuestario.PresupuestoVersion(IdPresupuestoVersion),
    CONSTRAINT FK_PresupuestoDetalle_CatalogoPartida FOREIGN KEY(IdCatalogoPartida)
        REFERENCES ControlPresupuestario.CatalogoPartida(IdCatalogoPartida),
    CONSTRAINT UQ_PresupuestoDetalle_Version_Partida UNIQUE(IdPresupuestoVersion, IdCatalogoPartida),
    CONSTRAINT CK_PresupuestoDetalle_MontoPresupuestado CHECK(MontoPresupuestado >= 0)
);
GO


/*
===============================================================================
ÍNDICE - PARTIDA
-------------------------------------------------------------------------------
Permite localizar rápidamente la misma partida a través de diferentes
versiones presupuestales.

Será especialmente útil para:

    - Comparar V1 vs V2.
    - Consultar evolución histórica de una partida.
    - Buscar presupuestos que utilizan determinada partida.
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_PresupuestoDetalle_IdCatalogoPartida
ON ControlPresupuestario.PresupuestoDetalle
(
    IdCatalogoPartida
)
INCLUDE
(
    IdPresupuestoVersion,
    MontoPresupuestado
);
GO


/*
===============================================================================
ÍNDICE - VERSION
-------------------------------------------------------------------------------
Optimiza la lectura completa de una versión presupuestal.

Ejemplo:

    SELECT ...
    FROM ControlPresupuestario.PresupuestoDetalle
    WHERE IdPresupuestoVersion = @IdPresupuestoVersion;

Esta será una de las consultas más frecuentes del módulo.
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_PresupuestoDetalle_IdPresupuestoVersion
ON ControlPresupuestario.PresupuestoDetalle
(
    IdPresupuestoVersion
)
INCLUDE
(
    IdCatalogoPartida,
    MontoPresupuestado,
    Observacion
);
GO