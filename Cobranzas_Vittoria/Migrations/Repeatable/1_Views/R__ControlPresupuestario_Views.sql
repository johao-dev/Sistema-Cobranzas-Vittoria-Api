/*
===============================================================================
REPEATABLE MIGRATION
-------------------------------------------------------------------------------
Módulo      : Control Presupuestario
Objeto      : vw_PresupuestoResumen
Descripción : Resumen económico por detalle presupuestario.
===============================================================================
*/
IF OBJECT_ID('ControlPresupuestario.vw_PresupuestoResumen', 'V') IS NOT NULL
    DROP VIEW ControlPresupuestario.vw_PresupuestoResumen;
GO

CREATE OR ALTER VIEW ControlPresupuestario.vw_PresupuestoResumen
AS
WITH Movimientos AS
(
    SELECT
        mp.IdPresupuestoDetalle,
        SUM
        (
            CASE WHEN tmp.Codigo = 'COMPROMISO' THEN mp.Monto
            ELSE 0
            END
        ) AS TotalCompromisos,
        SUM
        (
            CASE WHEN tmp.Codigo = 'LIBERACION' THEN mp.Monto
            ELSE 0
            END
        ) AS TotalLiberaciones,
        SUM
        (
            CASE WHEN tmp.Codigo = 'EJECUCION' THEN mp.Monto
            ELSE 0
            END
        ) AS TotalEjecutado,
        SUM
        (
            CASE WHEN tmp.Codigo = 'AJUSTE' THEN mp.Monto
            ELSE 0
            END
        ) AS TotalAjustes

    FROM ControlPresupuestario.MovimientoPresupuestal mp
    INNER JOIN ControlPresupuestario.TipoMovimientoPresupuestal tmp
        ON tmp.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
    GROUP BY mp.IdPresupuestoDetalle
)

SELECT
    cc.IdCentroCosto,
    cc.Codigo AS CodigoCentroCosto,
    cc.Nombre AS NombreCentroCosto,
    p.IdPresupuesto,
    p.Codigo AS CodigoPresupuesto,
    p.Nombre AS NombrePresupuesto,
    pv.IdPresupuestoVersion,
    pv.NumeroVersion,
    ep.IdEstadoPresupuesto,
    ep.Codigo AS EstadoPresupuesto,
    pd.IdPresupuestoDetalle,
    cp.IdCatalogoPartida,
    cp.Codigo AS CodigoPartida,
    cp.Nombre AS NombrePartida,
    cp.Nivel,
    pd.MontoPresupuestado,

    COALESCE(m.TotalCompromisos, 0) AS TotalCompromisos,
    COALESCE(m.TotalLiberaciones, 0) AS TotalLiberaciones,
    COALESCE(m.TotalEjecutado, 0) AS TotalEjecutado,
    COALESCE(m.TotalAjustes, 0) AS TotalAjustes,

    /*
        Comprometido pendiente:

        Compromisos
        - Liberaciones
        - Ejecuciones

        Nunca se expone como negativo.
    */
    CASE
        WHEN
            COALESCE(m.TotalCompromisos, 0)
            - COALESCE(m.TotalLiberaciones, 0)
            - COALESCE(m.TotalEjecutado, 0) > 0
        THEN
            COALESCE(m.TotalCompromisos, 0)
            - COALESCE(m.TotalLiberaciones, 0)
            - COALESCE(m.TotalEjecutado, 0)
        ELSE 0
    END AS MontoComprometido,

    /*
        Ejecutado representa gasto efectivamente realizado.
    */
    COALESCE(m.TotalEjecutado, 0) AS MontoEjecutado,

    /*
        Saldo disponible:

        Presupuesto
        - Compromiso pendiente
        - Ejecutado

        Los ajustes se exponen separadamente hasta definir
        explícitamente su semántica de incremento/reducción.
    */
    pd.MontoPresupuestado
    -
    CASE
        WHEN
            COALESCE(m.TotalCompromisos, 0)
            - COALESCE(m.TotalLiberaciones, 0)
            - COALESCE(m.TotalEjecutado, 0) > 0
        THEN
            COALESCE(m.TotalCompromisos, 0)
            - COALESCE(m.TotalLiberaciones, 0)
            - COALESCE(m.TotalEjecutado, 0)
        ELSE 0
    END
    -
    COALESCE(m.TotalEjecutado, 0) AS SaldoDisponible

FROM ControlPresupuestario.PresupuestoDetalle pd
INNER JOIN ControlPresupuestario.PresupuestoVersion pv
    ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
INNER JOIN ControlPresupuestario.EstadoPresupuesto ep
    ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
INNER JOIN ControlPresupuestario.Presupuesto p
    ON p.IdPresupuesto = pv.IdPresupuesto
INNER JOIN ControlPresupuestario.CentroCosto cc
    ON cc.IdCentroCosto = p.IdCentroCosto
INNER JOIN ControlPresupuestario.CatalogoPartida cp
    ON cp.IdCatalogoPartida = pd.IdCatalogoPartida
LEFT JOIN Movimientos m
    ON m.IdPresupuestoDetalle = pd.IdPresupuestoDetalle;
GO


/*
===============================================================================
Objeto      : vw_PresupuestoVsComprometido
Descripción : Comparación entre presupuesto y compromiso pendiente.
===============================================================================
*/
IF OBJECT_ID('ControlPresupuestario.vw_PresupuestoVsComprometido', 'V') IS NOT NULL
    DROP VIEW ControlPresupuestario.vw_PresupuestoVsComprometido;
GO

CREATE OR ALTER VIEW ControlPresupuestario.vw_PresupuestoVsComprometido
AS
SELECT
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdPresupuestoVersion,
    NumeroVersion,
    EstadoPresupuesto,
    IdPresupuestoDetalle,
    IdCatalogoPartida,
    CodigoPartida,
    NombrePartida,
    Nivel,
    MontoPresupuestado,
    MontoComprometido,
    MontoPresupuestado - MontoComprometido AS DiferenciaPresupuestoComprometido,

    CASE
        WHEN MontoPresupuestado = 0 THEN 0
        ELSE
            (MontoComprometido * 100.0 / MontoPresupuestado)
    END AS PorcentajeComprometido

FROM ControlPresupuestario.vw_PresupuestoResumen;
GO


/*
===============================================================================
Objeto      : vw_PresupuestoVsEjecutado
Descripción : Comparación entre presupuesto aprobado y ejecución real.
===============================================================================
*/
IF OBJECT_ID('ControlPresupuestario.vw_PresupuestoVsEjecutado', 'V') IS NOT NULL
    DROP VIEW ControlPresupuestario.vw_PresupuestoVsEjecutado;
GO

CREATE OR ALTER VIEW ControlPresupuestario.vw_PresupuestoVsEjecutado
AS
SELECT
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdPresupuestoVersion,
    NumeroVersion,
    EstadoPresupuesto,
    IdPresupuestoDetalle,
    IdCatalogoPartida,
    CodigoPartida,
    NombrePartida,
    Nivel,
    MontoPresupuestado,
    MontoEjecutado,
    MontoPresupuestado - MontoEjecutado AS DiferenciaPresupuestoEjecutado,

    CASE
        WHEN MontoPresupuestado = 0 THEN 0
        ELSE
            (MontoEjecutado * 100.0 / MontoPresupuestado)
    END AS PorcentajeEjecutado

FROM ControlPresupuestario.vw_PresupuestoResumen;
GO


/*
===============================================================================
Objeto      : vw_Saldo
Descripción : Saldo presupuestario disponible por partida.
===============================================================================
*/
IF OBJECT_ID('ControlPresupuestario.vw_Saldo', 'V') IS NOT NULL
    DROP VIEW ControlPresupuestario.vw_Saldo;
GO

CREATE OR ALTER VIEW ControlPresupuestario.vw_Saldo
AS
SELECT
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdPresupuestoVersion,
    NumeroVersion,
    EstadoPresupuesto,
    IdPresupuestoDetalle,
    IdCatalogoPartida,
    CodigoPartida,
    NombrePartida,
    Nivel,
    MontoPresupuestado,
    MontoComprometido,
    MontoEjecutado,
    SaldoDisponible,

    CASE
        WHEN SaldoDisponible < 0 THEN ABS(SaldoDisponible)
        ELSE 0
    END AS MontoExcedido,

    CASE
        WHEN SaldoDisponible < 0 THEN CAST(1 AS BIT)
        ELSE CAST(0 AS BIT)
    END AS Excedido

FROM ControlPresupuestario.vw_PresupuestoResumen;
GO


/*
===============================================================================
Objeto      : vw_GastosPorPartida
Descripción : Ejecución presupuestaria agrupada por partida.
===============================================================================
*/
IF OBJECT_ID('ControlPresupuestario.vw_GastosPorPartida', 'V') IS NOT NULL
    DROP VIEW ControlPresupuestario.vw_GastosPorPartida;
GO

CREATE OR ALTER VIEW ControlPresupuestario.vw_GastosPorPartida
AS
SELECT
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdPresupuestoVersion,
    NumeroVersion,
    EstadoPresupuesto,
    IdCatalogoPartida,
    CodigoPartida,
    NombrePartida,
    Nivel,
    SUM(MontoPresupuestado) AS MontoPresupuestado,
    SUM(MontoEjecutado) AS MontoEjecutado,
    SUM(MontoPresupuestado) - SUM(MontoEjecutado) AS Diferencia,

    CASE
        WHEN SUM(MontoPresupuestado) = 0 THEN 0
        ELSE
            (SUM(MontoEjecutado) * 100.0 / SUM(MontoPresupuestado))
    END AS PorcentajeEjecutado

FROM ControlPresupuestario.vw_PresupuestoResumen
GROUP BY
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdPresupuestoVersion,
    NumeroVersion,
    EstadoPresupuesto,
    IdCatalogoPartida,
    CodigoPartida,
    NombrePartida,
    Nivel;
GO


/*
===============================================================================
Objeto      : vw_GastosPorCentroCosto
Descripción : Resumen presupuestario agregado por centro de costo y versión.
===============================================================================
*/
IF OBJECT_ID('ControlPresupuestario.vw_GastosPorCentroCosto', 'V') IS NOT NULL
    DROP VIEW ControlPresupuestario.vw_GastosPorCentroCosto;
GO

CREATE OR ALTER VIEW ControlPresupuestario.vw_GastosPorCentroCosto
AS
SELECT
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdPresupuestoVersion,
    NumeroVersion,
    EstadoPresupuesto,
    SUM(MontoPresupuestado) AS MontoPresupuestado,
    SUM(MontoComprometido) AS MontoComprometido,
    SUM(MontoEjecutado) AS MontoEjecutado,
    SUM(SaldoDisponible) AS SaldoDisponible,
    CASE
        WHEN SUM(MontoPresupuestado) = 0 THEN 0
        ELSE
            (SUM(MontoEjecutado) * 100.0 / SUM(MontoPresupuestado))
    END AS PorcentajeEjecutado,

    CASE
        WHEN SUM(SaldoDisponible) < 0 THEN CAST(1 AS BIT)
        ELSE CAST(0 AS BIT)
    END AS Excedido

FROM ControlPresupuestario.vw_PresupuestoResumen
GROUP BY
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdPresupuestoVersion,
    NumeroVersion,
    EstadoPresupuesto;
GO
