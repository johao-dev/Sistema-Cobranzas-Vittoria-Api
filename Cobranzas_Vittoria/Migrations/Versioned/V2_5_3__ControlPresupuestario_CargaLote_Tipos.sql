/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.5.3
Módulo      : Control Presupuestario
Descripción : Tipo tabla para la carga en lote de montos de una versión
              (importación CSV y formulario de carga completa).
===============================================================================

La API resuelve el código de partida a IdCatalogoPartida antes de enviar el lote.
Observacion NULL conserva la observación que ya tenía el detalle.
_Fila identifica la fila del archivo (0 cuando el lote viene del formulario).
===============================================================================
*/

IF TYPE_ID(N'ControlPresupuestario.TVP_PresupuestoDetalleLote') IS NULL
    CREATE TYPE ControlPresupuestario.TVP_PresupuestoDetalleLote AS TABLE
    (
        IdCatalogoPartida   INT            NOT NULL,
        MontoPresupuestado  DECIMAL(18,2)  NOT NULL,
        Observacion         NVARCHAR(500)  NULL,
        _Fila               INT            NOT NULL
    );
GO
