/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.0.9
Módulo      : Control Presupuestario
Descripción : Integración del módulo Contable con Control Presupuestario.
Autor       : Johao Bravo
===============================================================================
*/


/*
===============================================================================
INTEGRACIÓN - GASTO PROYECTO
-------------------------------------------------------------------------------
Se agrega IdPresupuestoDetalle a contable.GastoProyecto.

Esto permite asociar un gasto registrado directamente contra un proyecto
con la partida presupuestaria que consume.

La columna se mantiene nullable para preservar compatibilidad con los
registros históricos existentes.

La obligatoriedad para nuevos registros sujetos a control presupuestario
será responsabilidad de la API.
===============================================================================
*/
ALTER TABLE contable.GastoProyecto ADD IdPresupuestoDetalle INT NULL;
GO

ALTER TABLE contable.GastoProyecto ADD CONSTRAINT FK_GastoProyecto_PresupuestoDetalle FOREIGN KEY(IdPresupuestoDetalle)
    REFERENCES ControlPresupuestario.PresupuestoDetalle(IdPresupuestoDetalle);
GO

CREATE NONCLUSTERED INDEX IX_GastoProyecto_IdPresupuestoDetalle ON contable.GastoProyecto(IdPresupuestoDetalle)
    WHERE IdPresupuestoDetalle IS NOT NULL;
GO


/*
===============================================================================
INTEGRACIÓN - GASTO ADMINISTRATIVO
-------------------------------------------------------------------------------
Se agrega IdPresupuestoDetalle a contable.GastoAdministrativo.

Esto permite asociar gastos corporativos o administrativos con su partida
presupuestaria correspondiente.

Ejemplos:

    - Servicios.
    - Marketing.
    - Administración.
    - Asesoría legal.
    - Gastos financieros.
    - Otros gastos corporativos.

Al igual que GastoProyecto, la columna permanece nullable para mantener
compatibilidad con registros históricos.
===============================================================================
*/
ALTER TABLE contable.GastoAdministrativo ADD IdPresupuestoDetalle INT NULL;
GO

ALTER TABLE contable.GastoAdministrativo ADD CONSTRAINT FK_GastoAdministrativo_PresupuestoDetalle FOREIGN KEY(IdPresupuestoDetalle)
    REFERENCES ControlPresupuestario.PresupuestoDetalle(IdPresupuestoDetalle);
GO


CREATE NONCLUSTERED INDEX IX_GastoAdministrativo_IdPresupuestoDetalle ON contable.GastoAdministrativo(IdPresupuestoDetalle)
    WHERE IdPresupuestoDetalle IS NOT NULL;
GO
