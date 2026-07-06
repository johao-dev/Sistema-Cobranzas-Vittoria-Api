-- Agregar columna Incidencia si no existe
IF COL_LENGTH('contable.PresupuestoProyectoDetalle', 'Incidencia') IS NULL
BEGIN
    ALTER TABLE contable.PresupuestoProyectoDetalle ADD Incidencia decimal(9,2) NULL;
END
GO