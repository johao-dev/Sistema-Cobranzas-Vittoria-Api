/*
===============================================================================
REPEATABLE MIGRATION
-------------------------------------------------------------------------------
Módulo      : Control Presupuestario
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