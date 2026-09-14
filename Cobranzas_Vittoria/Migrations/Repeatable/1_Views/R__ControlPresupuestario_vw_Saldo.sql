/*
===============================================================================
REPEATABLE MIGRATION
-------------------------------------------------------------------------------
Módulo      : Control Presupuestario
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
