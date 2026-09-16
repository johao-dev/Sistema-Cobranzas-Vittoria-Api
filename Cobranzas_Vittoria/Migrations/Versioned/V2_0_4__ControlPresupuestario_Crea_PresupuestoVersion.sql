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
Si existe una versión aprobada anterior, se copian todos sus detalles como
nuevas filas y luego se modifican los importes que correspondan. No se copian
movimientos. Los detalles no modificados conservan sus montos; cero significa
una asignación explícita de cero, no ausencia de cambios.
Una versión revisa la línea base del mismo presupuesto, sin reiniciar su ledger.

Los detalles presupuestales asociados a una versión se almacenarán
posteriormente en:

    ControlPresupuestario.PresupuestoDetalle

Reglas estructurales:

    - La numeración de versiones debe ser positiva.
    - Un mismo presupuesto no puede tener dos versiones con el mismo número.
    - Cada versión pertenece exactamente a un presupuesto.
    - Cada versión tiene un estado.

Reglas de negocio que serán responsabilidad de la API:

    - Una versión APROBADO no puede modificarse.
    - Una versión HISTORICO no puede modificarse.
    - Una versión ANULADO no puede modificarse.
    - Solo BORRADOR permite modificaciones.
    - Solo debe existir una versión APROBADO/vigente por presupuesto.
    - Transiciones: BORRADOR -> APROBADO -> HISTORICO; BORRADOR -> ANULADO.
    - ANULADO solo descarta un BORRADOR que nunca fue vigente. No se permiten
      APROBADO/HISTORICO -> ANULADO ni ANULADO -> APROBADO.
    - Al aprobar una nueva versión, la anterior APROBADO pasa a HISTORICO.
    - No aprobar un snapshot que omita partidas con compromiso pendiente,
      consumo acumulado relevante u operaciones históricas aún pendientes.
      Si se retira su asignación, conservar un detalle real con monto cero.
    - La validación y el reemplazo de la versión vigente deben ser atómicos
      y garantizar una única APROBADO frente a operaciones concurrentes.

Estas reglas de transición y cobertura no están implementadas en esta tabla.
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
