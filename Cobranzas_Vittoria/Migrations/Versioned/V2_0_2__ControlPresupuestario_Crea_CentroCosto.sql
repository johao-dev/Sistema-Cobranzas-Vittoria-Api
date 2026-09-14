/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.0.2
Módulo      : Control Presupuestario
Descripción : Creación de la entidad CentroCosto.
Autor       : Johao Bravo
===============================================================================
*/


/*
===============================================================================
CENTRO DE COSTO
-------------------------------------------------------------------------------
Representa la unidad organizacional o económica contra la cual se planifica,
controla y reporta el presupuesto.

Ejemplos:

    CC-PROY-001    Proyecto Talara 702
    CC-ADM-001     Administración
    CC-MKT-001     Marketing
    CC-VTA-001     Ventas

Un CentroCosto NO es un presupuesto.

El centro de costo identifica "dónde" se asignan y consumen recursos.
Los presupuestos asociados al centro de costo se crearán posteriormente
mediante ControlPresupuestario.Presupuesto.
===============================================================================
*/
CREATE TABLE ControlPresupuestario.CentroCosto
(
    IdCentroCosto INT IDENTITY(1,1) NOT NULL,
    Codigo VARCHAR(30) NOT NULL,
    Nombre NVARCHAR(150) NOT NULL,
    IdTipoCentroCosto INT NOT NULL,
    Descripcion NVARCHAR(255) NULL,
    Activo BIT NOT NULL CONSTRAINT DF_CentroCosto_Activo DEFAULT (1),
    FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_CentroCosto_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaModificacion DATETIME2(0) NULL CONSTRAINT DF_CentroCosto_FechaModificacion DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_CentroCosto PRIMARY KEY CLUSTERED (IdCentroCosto),
    CONSTRAINT UQ_CentroCosto_Codigo UNIQUE (Codigo),
    CONSTRAINT FK_CentroCosto_TipoCentroCosto FOREIGN KEY (IdTipoCentroCosto)
        REFERENCES ControlPresupuestario.TipoCentroCosto (IdTipoCentroCosto)
);
GO


/*
===============================================================================
ÍNDICES
-------------------------------------------------------------------------------
La FK necesita un índice explícito porque SQL Server no crea automáticamente
índices sobre claves foráneas.

Este índice será utilizado frecuentemente para consultas como:

    - Todos los centros de costo de tipo PROYECTO.
    - Todos los centros administrativos.
    - Reportes agrupados por tipo de centro de costo.

Activo se incluye como columna incluida porque será habitual filtrar o mostrar
el estado junto con el tipo.
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_CentroCosto_IdTipoCentroCosto
ON ControlPresupuestario.CentroCosto (IdTipoCentroCosto)
INCLUDE
(
    Codigo,
    Nombre,
    Activo
);
GO
