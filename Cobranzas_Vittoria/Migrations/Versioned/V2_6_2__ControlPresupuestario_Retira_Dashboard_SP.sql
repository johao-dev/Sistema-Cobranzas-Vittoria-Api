/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.6.2
Módulo      : Control Presupuestario
Descripción : Retira usp_Dashboard_Datos.
===============================================================================

El contrato de API establece que el reporting consume directamente las vistas,
sin SPs intermedios. El dashboard ahora se compone en la aplicación a partir de
vw_ControlPresupuestarioVigente y vw_EjecucionDiariaPorPartida.
===============================================================================
*/

DROP PROCEDURE IF EXISTS ControlPresupuestario.usp_Dashboard_Datos;
GO
