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
