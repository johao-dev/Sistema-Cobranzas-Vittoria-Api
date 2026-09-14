/*
===============================================================================
REPEATABLE MIGRATION
-------------------------------------------------------------------------------
Módulo      : Control Presupuestario
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