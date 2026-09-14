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

    Compra o gasto registrado
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
    IdTipoMovimientoPresupuestal INT NOT NULL,
    Origen VARCHAR(50) NOT NULL,
    IdOrigen INT NOT NULL,
    Fecha DATETIME2(0) NOT NULL CONSTRAINT DF_MovimientoPresupuestal_Fecha DEFAULT (SYSDATETIME()),
    Monto DECIMAL(18,2) NOT NULL,
    Observacion NVARCHAR(500) NULL,

    CONSTRAINT PK_MovimientoPresupuestal PRIMARY KEY CLUSTERED(IdMovimientoPresupuestal),
    CONSTRAINT FK_MovimientoPresupuestal_PresupuestoDetalle FOREIGN KEY(IdPresupuestoDetalle)
        REFERENCES ControlPresupuestario.PresupuestoDetalle(IdPresupuestoDetalle),
    CONSTRAINT FK_MovimientoPresupuestal_TipoMovimiento FOREIGN KEY(IdTipoMovimientoPresupuestal)
        REFERENCES ControlPresupuestario.TipoMovimientoPresupuestal(IdTipoMovimientoPresupuestal),
    CONSTRAINT CK_MovimientoPresupuestal_Monto CHECK(Monto > 0),
    CONSTRAINT CK_MovimientoPresupuestal_Origen CHECK(LEN(LTRIM(RTRIM(Origen))) > 0),
    CONSTRAINT CK_MovimientoPresupuestal_IdOrigen CHECK(IdOrigen > 0)
);
GO


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