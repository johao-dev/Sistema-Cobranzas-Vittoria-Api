/*
===============================================================================
REPEATABLE MIGRATION
-------------------------------------------------------------------------------
Módulo      : Control Presupuestario
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
