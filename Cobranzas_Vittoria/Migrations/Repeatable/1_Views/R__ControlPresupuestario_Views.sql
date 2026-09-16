/*
===============================================================================
REPEATABLE MIGRATION - CONTROL PRESUPUESTARIO
-------------------------------------------------------------------------------
Las seis vistas originales son HISTORICAS POR VERSION.
vw_ControlPresupuestarioVigente muestra la línea base aprobada y el efecto neto
acumulado de la cadena APROBADO/HISTORICO del mismo presupuesto y partida.
Todos los montos están expresados en la moneda del presupuesto.
CREATE OR ALTER conserva permisos y evita eliminar objetos en cada arranque.
===============================================================================
*/
CREATE OR ALTER VIEW ControlPresupuestario.vw_PresupuestoResumen
AS
WITH Movimientos AS
(
    SELECT
        mp.IdPresupuestoDetalle,
        CAST(SUM(CASE WHEN tmp.Codigo = 'COMPROMISO'
            THEN mp.Monto ELSE 0 END) AS DECIMAL(28,2)) AS TotalCompromisos,
        CAST(SUM(CASE WHEN tmp.Codigo = 'LIBERACION'
            THEN mp.Monto ELSE 0 END) AS DECIMAL(28,2)) AS TotalLiberaciones,
        CAST(SUM(CASE WHEN tmp.Codigo = 'EJECUCION'
            THEN mp.Monto ELSE 0 END) AS DECIMAL(28,2)) AS TotalEjecutado,
        CAST(SUM(CASE WHEN tmp.Codigo = 'AJUSTE'
            THEN mp.Monto ELSE 0 END) AS DECIMAL(28,2)) AS TotalAjustes,
        CAST(SUM(CASE WHEN tmp.Codigo = 'AJUSTE' AND mp.Afectacion = 'COMPROMISO' AND mp.Direccion = 'INCREMENTO'
            THEN mp.Monto ELSE 0 END) AS DECIMAL(28,2)) AS TotalAjustesCompromisoIncremento,
        CAST(SUM(CASE WHEN tmp.Codigo = 'AJUSTE' AND mp.Afectacion = 'COMPROMISO' AND mp.Direccion = 'DECREMENTO'
            THEN mp.Monto ELSE 0 END) AS DECIMAL(28,2)) AS TotalAjustesCompromisoDecremento,
        CAST(SUM(CASE WHEN tmp.Codigo = 'AJUSTE' AND mp.Afectacion = 'EJECUCION' AND mp.Direccion = 'INCREMENTO'
            THEN mp.Monto ELSE 0 END) AS DECIMAL(28,2)) AS TotalAjustesEjecucionIncremento,
        CAST(SUM(CASE WHEN tmp.Codigo = 'AJUSTE' AND mp.Afectacion = 'EJECUCION' AND mp.Direccion = 'DECREMENTO'
            THEN mp.Monto ELSE 0 END) AS DECIMAL(28,2)) AS TotalAjustesEjecucionDecremento
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
    mon.IdMoneda,
    mon.Codigo AS CodigoMoneda,
    mon.Simbolo AS SimboloMoneda,
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
    COALESCE(m.TotalAjustesCompromisoIncremento, 0) AS TotalAjustesCompromisoIncremento,
    COALESCE(m.TotalAjustesCompromisoDecremento, 0) AS TotalAjustesCompromisoDecremento,
    COALESCE(m.TotalAjustesEjecucionIncremento, 0) AS TotalAjustesEjecucionIncremento,
    COALESCE(m.TotalAjustesEjecucionDecremento, 0) AS TotalAjustesEjecucionDecremento,
    economia.MontoComprometido,
    economia.MontoEjecutado,
    pd.MontoPresupuestado - economia.MontoComprometido
        - economia.MontoEjecutado AS SaldoDisponible
FROM ControlPresupuestario.PresupuestoDetalle pd
INNER JOIN ControlPresupuestario.PresupuestoVersion pv
    ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
INNER JOIN ControlPresupuestario.EstadoPresupuesto ep
    ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
INNER JOIN ControlPresupuestario.Presupuesto p
    ON p.IdPresupuesto = pv.IdPresupuesto
INNER JOIN ControlPresupuestario.Moneda mon
    ON mon.IdMoneda = p.IdMoneda
INNER JOIN ControlPresupuestario.CentroCosto cc
    ON cc.IdCentroCosto = p.IdCentroCosto
INNER JOIN ControlPresupuestario.CatalogoPartida cp
    ON cp.IdCatalogoPartida = pd.IdCatalogoPartida
LEFT JOIN Movimientos m
    ON m.IdPresupuestoDetalle = pd.IdPresupuestoDetalle
CROSS APPLY
(
    SELECT
        COALESCE(m.TotalCompromisos, 0) - COALESCE(m.TotalLiberaciones, 0)
            + COALESCE(m.TotalAjustesCompromisoIncremento, 0)
            - COALESCE(m.TotalAjustesCompromisoDecremento, 0) AS MontoComprometido,
        COALESCE(m.TotalEjecutado, 0)
            + COALESCE(m.TotalAjustesEjecucionIncremento, 0)
            - COALESCE(m.TotalAjustesEjecucionDecremento, 0) AS MontoEjecutado
) economia;
GO


/*
===============================================================================
Objeto      : vw_PresupuestoVsComprometido
Descripción : Comparación entre presupuesto y compromiso pendiente.
===============================================================================
*/
CREATE OR ALTER VIEW ControlPresupuestario.vw_PresupuestoVsComprometido
AS
SELECT
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdMoneda,
    CodigoMoneda,
    SimboloMoneda,
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
Descripción : Comparación histórica entre presupuesto de versión y ejecución neta.
===============================================================================
*/
CREATE OR ALTER VIEW ControlPresupuestario.vw_PresupuestoVsEjecutado
AS
SELECT
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdMoneda,
    CodigoMoneda,
    SimboloMoneda,
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
CREATE OR ALTER VIEW ControlPresupuestario.vw_Saldo
AS
SELECT
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdMoneda,
    CodigoMoneda,
    SimboloMoneda,
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
CREATE OR ALTER VIEW ControlPresupuestario.vw_GastosPorPartida
AS
SELECT
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdMoneda,
    CodigoMoneda,
    SimboloMoneda,
    IdPresupuestoVersion,
    NumeroVersion,
    EstadoPresupuesto,
    IdCatalogoPartida,
    CodigoPartida,
    NombrePartida,
    Nivel,
    CAST(SUM(MontoPresupuestado) AS DECIMAL(28,2)) AS MontoPresupuestado,
    CAST(SUM(MontoEjecutado) AS DECIMAL(28,2)) AS MontoEjecutado,
    CAST(SUM(MontoPresupuestado) AS DECIMAL(28,2))
        - CAST(SUM(MontoEjecutado) AS DECIMAL(28,2)) AS Diferencia,

    CASE
        WHEN SUM(MontoPresupuestado) = 0 THEN 0
        ELSE
            (CAST(SUM(MontoEjecutado) AS DECIMAL(28,2)) * 100.0
                / CAST(SUM(MontoPresupuestado) AS DECIMAL(28,2)))
    END AS PorcentajeEjecutado

FROM ControlPresupuestario.vw_PresupuestoResumen
GROUP BY
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdMoneda,
    CodigoMoneda,
    SimboloMoneda,
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
CREATE OR ALTER VIEW ControlPresupuestario.vw_GastosPorCentroCosto
AS
SELECT
    IdCentroCosto,
    CodigoCentroCosto,
    NombreCentroCosto,
    IdPresupuesto,
    CodigoPresupuesto,
    NombrePresupuesto,
    IdMoneda,
    CodigoMoneda,
    SimboloMoneda,
    IdPresupuestoVersion,
    NumeroVersion,
    EstadoPresupuesto,
    CAST(SUM(MontoPresupuestado) AS DECIMAL(28,2)) AS MontoPresupuestado,
    CAST(SUM(MontoComprometido) AS DECIMAL(28,2)) AS MontoComprometido,
    CAST(SUM(MontoEjecutado) AS DECIMAL(28,2)) AS MontoEjecutado,
    CAST(SUM(SaldoDisponible) AS DECIMAL(28,2)) AS SaldoDisponible,
    CASE
        WHEN SUM(MontoPresupuestado) = 0 THEN 0
        ELSE
            (CAST(SUM(MontoEjecutado) AS DECIMAL(28,2)) * 100.0
                / CAST(SUM(MontoPresupuestado) AS DECIMAL(28,2)))
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
    IdMoneda,
    CodigoMoneda,
    SimboloMoneda,
    IdPresupuestoVersion,
    NumeroVersion,
    EstadoPresupuesto;
GO


/*
===============================================================================
Objeto      : vw_ControlPresupuestarioVigente
Descripción : Línea base aprobada frente al ledger neto acumulado.
-------------------------------------------------------------------------------
No suma montos presupuestados históricos ni reasigna movimientos.
BORRADOR/ANULADO no aportan al arrastre. ANULADO se considera una versión
descartada que nunca fue efectiva hasta definir una política distinta.
Una partida ausente del snapshot actual con efecto neto histórico no nulo
se expone con presupuesto cero e IdPresupuestoDetalle NULL para no ocultar consumo.
===============================================================================
*/
CREATE OR ALTER VIEW ControlPresupuestario.vw_ControlPresupuestarioVigente
AS
WITH VersionesVigentes AS
(
    SELECT pv.IdPresupuestoVersion, pv.IdPresupuesto, pv.NumeroVersion,
        pv.IdEstadoPresupuesto
    FROM ControlPresupuestario.PresupuestoVersion pv
    INNER JOIN ControlPresupuestario.EstadoPresupuesto ep
        ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
    WHERE ep.Codigo = 'APROBADO'
),
Movimientos AS
(
    SELECT r.IdPresupuesto, r.IdCatalogoPartida,
        CAST(SUM(r.TotalCompromisos) AS DECIMAL(28,2)) AS TotalCompromisos,
        CAST(SUM(r.TotalLiberaciones) AS DECIMAL(28,2)) AS TotalLiberaciones,
        CAST(SUM(r.TotalEjecutado) AS DECIMAL(28,2)) AS TotalEjecutado,
        CAST(SUM(r.TotalAjustes) AS DECIMAL(28,2)) AS TotalAjustes,
        CAST(SUM(r.TotalAjustesCompromisoIncremento) AS DECIMAL(28,2)) AS TotalAjustesCompromisoIncremento,
        CAST(SUM(r.TotalAjustesCompromisoDecremento) AS DECIMAL(28,2)) AS TotalAjustesCompromisoDecremento,
        CAST(SUM(r.TotalAjustesEjecucionIncremento) AS DECIMAL(28,2)) AS TotalAjustesEjecucionIncremento,
        CAST(SUM(r.TotalAjustesEjecucionDecremento) AS DECIMAL(28,2)) AS TotalAjustesEjecucionDecremento,
        CAST(SUM(r.MontoComprometido) AS DECIMAL(28,2)) AS MontoComprometido,
        CAST(SUM(r.MontoEjecutado) AS DECIMAL(28,2)) AS MontoEjecutado
    FROM ControlPresupuestario.vw_PresupuestoResumen r
    WHERE r.EstadoPresupuesto IN ('APROBADO', 'HISTORICO')
    GROUP BY r.IdPresupuesto, r.IdCatalogoPartida
),
Partidas AS
(
    SELECT v.IdPresupuestoVersion, pd.IdCatalogoPartida,
        pd.IdPresupuestoDetalle, pd.MontoPresupuestado,
        CAST(1 AS BIT) AS EsPartidaEnVersionVigente
    FROM VersionesVigentes v
    INNER JOIN ControlPresupuestario.PresupuestoDetalle pd
        ON pd.IdPresupuestoVersion = v.IdPresupuestoVersion

    UNION ALL

    SELECT v.IdPresupuestoVersion, m.IdCatalogoPartida,
        CAST(NULL AS INT) AS IdPresupuestoDetalle,
        CAST(0 AS DECIMAL(18,2)) AS MontoPresupuestado,
        CAST(0 AS BIT) AS EsPartidaEnVersionVigente
    FROM VersionesVigentes v
    INNER JOIN Movimientos m ON m.IdPresupuesto = v.IdPresupuesto
    WHERE (m.MontoComprometido <> 0 OR m.MontoEjecutado <> 0)
        AND NOT EXISTS
        (
            SELECT 1
            FROM ControlPresupuestario.PresupuestoDetalle pd
            WHERE pd.IdPresupuestoVersion = v.IdPresupuestoVersion
                AND pd.IdCatalogoPartida = m.IdCatalogoPartida
        )
)
SELECT
    cc.IdCentroCosto,
    cc.Codigo AS CodigoCentroCosto,
    cc.Nombre AS NombreCentroCosto,
    p.IdPresupuesto,
    p.Codigo AS CodigoPresupuesto,
    p.Nombre AS NombrePresupuesto,
    mon.IdMoneda,
    mon.Codigo AS CodigoMoneda,
    mon.Simbolo AS SimboloMoneda,
    v.IdPresupuestoVersion,
    v.NumeroVersion,
    v.IdEstadoPresupuesto,
    ep.Codigo AS EstadoPresupuesto,
    actual.IdPresupuestoDetalle,
    actual.EsPartidaEnVersionVigente,
    cp.IdCatalogoPartida,
    cp.Codigo AS CodigoPartida,
    cp.Nombre AS NombrePartida,
    cp.Nivel,
    actual.MontoPresupuestado,
    COALESCE(m.TotalCompromisos, 0) AS TotalCompromisos,
    COALESCE(m.TotalLiberaciones, 0) AS TotalLiberaciones,
    COALESCE(m.TotalEjecutado, 0) AS TotalEjecutado,
    COALESCE(m.TotalAjustes, 0) AS TotalAjustes,
    COALESCE(m.TotalAjustesCompromisoIncremento, 0) AS TotalAjustesCompromisoIncremento,
    COALESCE(m.TotalAjustesCompromisoDecremento, 0) AS TotalAjustesCompromisoDecremento,
    COALESCE(m.TotalAjustesEjecucionIncremento, 0) AS TotalAjustesEjecucionIncremento,
    COALESCE(m.TotalAjustesEjecucionDecremento, 0) AS TotalAjustesEjecucionDecremento,
    COALESCE(m.MontoComprometido, 0) AS MontoComprometido,
    COALESCE(m.MontoEjecutado, 0) AS MontoEjecutado,
    saldo.SaldoDisponible,
    CASE WHEN saldo.SaldoDisponible < 0 THEN ABS(saldo.SaldoDisponible)
        ELSE 0 END AS MontoExcedido,
    CAST(CASE WHEN saldo.SaldoDisponible < 0 THEN 1 ELSE 0 END AS BIT) AS Excedido
FROM VersionesVigentes v
INNER JOIN Partidas actual ON actual.IdPresupuestoVersion = v.IdPresupuestoVersion
INNER JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = v.IdPresupuesto
INNER JOIN ControlPresupuestario.Moneda mon ON mon.IdMoneda = p.IdMoneda
INNER JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
INNER JOIN ControlPresupuestario.EstadoPresupuesto ep
    ON ep.IdEstadoPresupuesto = v.IdEstadoPresupuesto
INNER JOIN ControlPresupuestario.CatalogoPartida cp
    ON cp.IdCatalogoPartida = actual.IdCatalogoPartida
LEFT JOIN Movimientos m
    ON m.IdPresupuesto = v.IdPresupuesto
        AND m.IdCatalogoPartida = actual.IdCatalogoPartida
CROSS APPLY
(
    SELECT actual.MontoPresupuestado - COALESCE(m.MontoComprometido, 0)
        - COALESCE(m.MontoEjecutado, 0) AS SaldoDisponible
) saldo;
GO
