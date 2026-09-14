/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.0.8
Módulo      : Control Presupuestario
Descripción : Integración del módulo de Compras con Control Presupuestario.
Autor       : Johao Bravo
===============================================================================
*/


/*
===============================================================================
INTEGRACIÓN - REQUERIMIENTO
-------------------------------------------------------------------------------
Se agrega IdPresupuestoDetalle a compras.Requerimiento.

El requerimiento representa el punto de origen donde el usuario identifica
contra qué partida del presupuesto se realizará la solicitud.

Flujo:

    PresupuestoDetalle
            ▲
            │
    Requerimiento
            │
            ▼
      OrdenCompra
            │
            ▼
         Compra

No se agrega IdPresupuestoDetalle a OrdenCompra ni Compra, ya que ambas
entidades pueden obtener la referencia presupuestaria a través del
Requerimiento que originó el flujo.

La columna se crea inicialmente como NULL para mantener compatibilidad con:

    - Requerimientos históricos.
    - Requerimientos existentes.
    - Procesos que todavía no hayan sido migrados al control presupuestario.

La obligatoriedad para nuevos requerimientos será una regla de negocio
implementada posteriormente en la API.
===============================================================================
*/
ALTER TABLE compras.Requerimiento
ADD IdPresupuestoDetalle INT NULL;
GO


/*
===============================================================================
FOREIGN KEY
===============================================================================
*/
ALTER TABLE compras.Requerimiento ADD CONSTRAINT FK_Requerimiento_PresupuestoDetalle FOREIGN KEY(IdPresupuestoDetalle)
    REFERENCES ControlPresupuestario.PresupuestoDetalle(IdPresupuestoDetalle);
GO


/*
===============================================================================
ÍNDICE
-------------------------------------------------------------------------------
Facilita obtener los requerimientos asociados a una partida presupuestaria.

Será útil para:

    - Trazabilidad presupuestaria.
    - Consultar requerimientos por partida.
    - Navegar desde ControlPresupuestario hacia Compras.
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_Requerimiento_IdPresupuestoDetalle ON compras.Requerimiento(IdPresupuestoDetalle)
    WHERE IdPresupuestoDetalle IS NOT NULL;
GO