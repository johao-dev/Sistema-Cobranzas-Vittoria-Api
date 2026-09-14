/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.0.4
Módulo      : Control Presupuestario
Descripción : Creación de la entidad PresupuestoVersion.
Autor       : Johao Bravo
===============================================================================
*/


/*
===============================================================================
PRESUPUESTO VERSION
-------------------------------------------------------------------------------
Representa una versión concreta de un presupuesto.

Ejemplo:

    Presupuesto Talara 702
        V1 -> HISTORICO
        V2 -> APROBADO
        V3 -> BORRADOR

Cada versión funcionará como cabecera de un snapshot completo.

Los detalles presupuestales asociados a una versión se almacenarán
posteriormente en:

    ControlPresupuestario.PresupuestoDetalle

Reglas estructurales:

    - La numeración de versiones debe ser positiva.
    - Un mismo presupuesto no puede tener dos versiones con el mismo número.
    - Cada versión pertenece exactamente a un presupuesto.
    - Cada versión tiene un estado.

Reglas de negocio que serán responsabilidad de la API:

    - Una versión APROBADA no puede modificarse.
    - Una versión HISTORICO no puede modificarse.
    - Una versión ANULADA no puede modificarse.
    - Solo BORRADOR permite modificaciones.
    - Solo debe existir una versión APROBADA/vigente por presupuesto.
    - Una nueva versión normalmente se genera copiando la versión aprobada.
===============================================================================
*/
CREATE TABLE ControlPresupuestario.PresupuestoVersion
(
    IdPresupuestoVersion INT IDENTITY(1,1) NOT NULL,
    IdPresupuesto INT NOT NULL,
    IdEstadoPresupuesto INT NOT NULL,
    NumeroVersion INT NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    MotivoCambio NVARCHAR(500) NULL,
    FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_PresupuestoVersion_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaAprobacion DATETIME2(0) NULL,
    UsuarioCreacion NVARCHAR(100) NULL,
    UsuarioAprobacion NVARCHAR(100) NULL,

    CONSTRAINT PK_PresupuestoVersion PRIMARY KEY CLUSTERED (IdPresupuestoVersion),
    CONSTRAINT FK_PresupuestoVersion_Presupuesto FOREIGN KEY (IdPresupuesto)
        REFERENCES ControlPresupuestario.Presupuesto(IdPresupuesto),
    CONSTRAINT FK_PresupuestoVersion_EstadoPresupuesto FOREIGN KEY (IdEstadoPresupuesto)
        REFERENCES ControlPresupuestario.EstadoPresupuesto(IdEstadoPresupuesto),
    CONSTRAINT UQ_PresupuestoVersion_Presupuesto_NumeroVersion UNIQUE (IdPresupuesto, NumeroVersion),
    CONSTRAINT CK_PresupuestoVersion_NumeroVersion CHECK (NumeroVersion > 0)
);
GO


/*
===============================================================================
ÍNDICE - PRESUPUESTO / ESTADO
-------------------------------------------------------------------------------
Facilita consultas como:

    - Obtener versiones de un presupuesto.
    - Obtener la versión aprobada/vigente.
    - Obtener borradores.
    - Consultar historial de versiones.

NumeroVersion DESC facilita devolver primero las versiones más recientes.
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_PresupuestoVersion_Presupuesto_Estado
ON ControlPresupuestario.PresupuestoVersion
(
    IdPresupuesto,
    IdEstadoPresupuesto,
    NumeroVersion DESC
)
INCLUDE
(
    FechaCreacion,
    FechaAprobacion
);
GO


/*
===============================================================================
ÍNDICE - VERSIONES POR PRESUPUESTO
-------------------------------------------------------------------------------
Optimiza el historial y la obtención de la última versión existente.

Ejemplo:

    SELECT TOP 1 ...
    WHERE IdPresupuesto = @IdPresupuesto
    ORDER BY NumeroVersion DESC;
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_PresupuestoVersion_Presupuesto_NumeroVersion
ON ControlPresupuestario.PresupuestoVersion
(
    IdPresupuesto,
    NumeroVersion DESC
)
INCLUDE
(
    IdEstadoPresupuesto,
    FechaCreacion,
    FechaAprobacion
);
GO