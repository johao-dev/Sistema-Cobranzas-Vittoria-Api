/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.0.3
Módulo      : Control Presupuestario
Descripción : Creación de la entidad Presupuesto.
Autor       : Johao Bravo
===============================================================================
*/


/*
===============================================================================
PRESUPUESTO
-------------------------------------------------------------------------------
Representa la identidad lógica de un presupuesto asociado a un CentroCosto.

Esta tabla NO almacena:
    - Número de versión.
    - Estado de una versión.
    - Montos presupuestados.
    - Partidas.

Estos datos pertenecen a PresupuestoVersion y PresupuestoDetalle.

Ejemplos:

    PRE-2027-TAL702    Presupuesto de Obra 2027 - Talara 702
    PRE-2027-ADM       Presupuesto Administrativo 2027
    PRE-2027-MKT       Presupuesto de Marketing 2027

Un presupuesto puede tener múltiples versiones durante su ciclo de vida.
IdMoneda define la moneda de todos sus detalles y movimientos. Cambiar la
moneda de un presupuesto con versiones requiere crear otro presupuesto.
===============================================================================
*/
CREATE TABLE ControlPresupuestario.Presupuesto
(
    IdPresupuesto INT IDENTITY(1,1) NOT NULL,
    IdCentroCosto INT NOT NULL,
    IdMoneda INT NOT NULL,
    Codigo VARCHAR(50) NOT NULL,
    Nombre NVARCHAR(200) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    FechaInicio DATE NULL,
    FechaFin DATE NULL,
    Activo BIT NOT NULL CONSTRAINT DF_Presupuesto_Activo DEFAULT (1),
    FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_Presupuesto_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaModificacion DATETIME2(0) NULL,

    CONSTRAINT PK_Presupuesto PRIMARY KEY CLUSTERED (IdPresupuesto),
    CONSTRAINT FK_Presupuesto_CentroCosto FOREIGN KEY (IdCentroCosto)
        REFERENCES ControlPresupuestario.CentroCosto(IdCentroCosto),
    CONSTRAINT FK_Presupuesto_Moneda FOREIGN KEY (IdMoneda)
        REFERENCES ControlPresupuestario.Moneda(IdMoneda),
    CONSTRAINT UQ_Presupuesto_Codigo UNIQUE (Codigo),
    CONSTRAINT CK_Presupuesto_RangoFechas CHECK
    (
        FechaInicio IS NULL
        OR FechaFin IS NULL
        OR FechaFin >= FechaInicio
    )
);
GO


/*
===============================================================================
ÍNDICES
-------------------------------------------------------------------------------
La consulta de presupuestos por centro de costo será una de las operaciones
más habituales del módulo.

Ejemplos:

    - Presupuestos del proyecto Talara 702.
    - Presupuestos de Administración.
    - Presupuestos históricos de un CentroCosto.
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_Presupuesto_IdCentroCosto
ON ControlPresupuestario.Presupuesto
(
    IdCentroCosto
)
INCLUDE
(
    IdMoneda,
    Codigo,
    Nombre,
    FechaInicio,
    FechaFin,
    Activo
);
GO

CREATE NONCLUSTERED INDEX IX_Presupuesto_IdMoneda
ON ControlPresupuestario.Presupuesto (IdMoneda);
GO
