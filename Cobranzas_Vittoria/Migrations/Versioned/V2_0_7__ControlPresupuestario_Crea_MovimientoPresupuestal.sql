/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.0.7
Módulo      : Control Presupuestario
Descripción : Creación del ledger de movimientos presupuestales.
Autor       : Johao Bravo
===============================================================================
*/


/*
===============================================================================
MOVIMIENTO PRESUPUESTAL
-------------------------------------------------------------------------------
Registra los efectos económicos producidos sobre un PresupuestoDetalle.

Ejemplos:

    Orden de compra aprobada
        -> COMPROMISO

    Orden de compra cancelada/reducida
        -> LIBERACION

    Compra aceptada o gasto directo activo
        -> EJECUCION

    Corrección presupuestaria controlada
        -> AJUSTE


La tabla funciona como un ledger.

Los movimientos económicos registrados NO deben modificarse o eliminarse para
representar anulaciones.

En su lugar se registra un nuevo movimiento compensatorio.

Ejemplo:

    COMPROMISO    S/ 50,000
    LIBERACION    S/ 50,000


Origen + IdOrigen permiten conocer qué operación del sistema produjo el
movimiento.

Ejemplos:

    Origen = 'ORDEN_COMPRA'
    IdOrigen = 125

    Origen = 'COMPRA'
    IdOrigen = 90

    Origen = 'GASTO_PROYECTO'
    IdOrigen = 41

    Origen = 'GASTO_ADMINISTRATIVO'
    IdOrigen = 20
===============================================================================
*/
CREATE TABLE ControlPresupuestario.MovimientoPresupuestal
(
    IdMovimientoPresupuestal BIGINT IDENTITY(1,1) NOT NULL,
    IdPresupuestoDetalle INT NOT NULL,
    ClaveEvento VARCHAR(200) NOT NULL,
    IdTipoMovimientoPresupuestal INT NOT NULL,
    Origen VARCHAR(50) NOT NULL,
    IdOrigen INT NOT NULL,
    Afectacion VARCHAR(20) NULL,
    Direccion VARCHAR(20) NULL,
    Fecha DATETIME2(0) NOT NULL CONSTRAINT DF_MovimientoPresupuestal_Fecha DEFAULT (SYSDATETIME()),
    Monto DECIMAL(18,2) NOT NULL,
    Observacion NVARCHAR(500) NULL,

    CONSTRAINT PK_MovimientoPresupuestal PRIMARY KEY CLUSTERED(IdMovimientoPresupuestal),
    CONSTRAINT FK_MovimientoPresupuestal_PresupuestoDetalle FOREIGN KEY(IdPresupuestoDetalle)
        REFERENCES ControlPresupuestario.PresupuestoDetalle(IdPresupuestoDetalle),
    CONSTRAINT FK_MovimientoPresupuestal_TipoMovimiento FOREIGN KEY(IdTipoMovimientoPresupuestal)
        REFERENCES ControlPresupuestario.TipoMovimientoPresupuestal(IdTipoMovimientoPresupuestal),
    CONSTRAINT UQ_MovimientoPresupuestal_ClaveEvento UNIQUE(ClaveEvento),
    CONSTRAINT CK_MovimientoPresupuestal_ClaveEvento CHECK(LEN(LTRIM(RTRIM(ClaveEvento))) > 0),
    CONSTRAINT CK_MovimientoPresupuestal_Afectacion CHECK
        (Afectacion IS NULL OR Afectacion IN ('COMPROMISO', 'EJECUCION')),
    CONSTRAINT CK_MovimientoPresupuestal_Direccion CHECK
        (Direccion IS NULL OR Direccion IN ('INCREMENTO', 'DECREMENTO')),
    CONSTRAINT CK_MovimientoPresupuestal_Monto CHECK(Monto > 0),
    CONSTRAINT CK_MovimientoPresupuestal_Origen CHECK(LEN(LTRIM(RTRIM(Origen))) > 0),
    CONSTRAINT CK_MovimientoPresupuestal_IdOrigen CHECK(IdOrigen > 0)
);
GO

/*
La coherencia entre el tipo y Afectacion/Direccion se validará en la API:
AJUSTE exige ambas dimensiones; los demás tipos exigen ambas en NULL.
La DB solo restringe los valores permitidos de cada dimensión por separado.
La validación de API está pendiente de implementación; no se genera un CHECK
dependiente del ID del catálogo ni se utiliza SQL dinámico para esa regla.
*/


/*
===============================================================================
ÍNDICE - MOVIMIENTOS POR DETALLE PRESUPUESTAL
-------------------------------------------------------------------------------
Será uno de los índices principales para calcular:

    - Comprometido.
    - Ejecutado.
    - Liberado.
    - Saldo disponible.

También permitirá consultar el historial de una partida presupuestaria.
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_MovimientoPresupuestal_PresupuestoDetalle
ON ControlPresupuestario.MovimientoPresupuestal
(
    IdPresupuestoDetalle,
    IdTipoMovimientoPresupuestal
)
INCLUDE
(
    Monto,
    Afectacion,
    Direccion,
    Fecha,
    Origen,
    IdOrigen
);
GO


/*
===============================================================================
ÍNDICE - ORIGEN
-------------------------------------------------------------------------------
Permite localizar rápidamente los movimientos generados por una operación
del sistema.

Ejemplos:

    ORDEN_COMPRA + 125
    COMPRA + 90
    GASTO_PROYECTO + 41
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_MovimientoPresupuestal_Origen
ON ControlPresupuestario.MovimientoPresupuestal
(
    Origen,
    IdOrigen
)
INCLUDE
(
    IdPresupuestoDetalle,
    IdTipoMovimientoPresupuestal,
    Monto,
    Fecha
);
GO


/*
===============================================================================
ÍNDICE - FECHA
-------------------------------------------------------------------------------
Facilita reportes presupuestarios por periodo.

Ejemplos:

    - Ejecución mensual.
    - Compromisos del periodo.
    - Evolución presupuestaria.
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_MovimientoPresupuestal_Fecha
ON ControlPresupuestario.MovimientoPresupuestal
(
    Fecha
)
INCLUDE
(
    IdPresupuestoDetalle,
    IdTipoMovimientoPresupuestal,
    Monto
);
GO
