/*
===============================================================================
REPEATABLE MIGRATION
-------------------------------------------------------------------------------
Módulo      : Control Presupuestario
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
