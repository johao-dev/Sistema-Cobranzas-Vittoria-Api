/*
Iteración actual: habilita transiciones explícitas para el flujo de requerimientos
sin eliminar ni reinterpretar datos históricos.

Deuda técnica planificada: este modelo mantiene el estado como texto en la tabla
Requerimiento para reducir el impacto de esta entrega. En un refactor posterior se
deberá normalizar el catálogo/transiciones y registrar un historial de estados;
en código, el flujo podrá migrar al patrón State para que las reglas no vivan en
procedimientos almacenados dispersos.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @constraintName sysname;
SELECT @constraintName = kc.name
FROM sys.check_constraints kc
INNER JOIN sys.columns c
    ON c.object_id = kc.parent_object_id
   AND c.column_id = kc.parent_column_id
WHERE kc.parent_object_id = OBJECT_ID(N'compras.Requerimiento')
  AND c.name = N'Estado';

IF @constraintName IS NOT NULL
BEGIN
    DECLARE @dropConstraintSql nvarchar(max) =
        N'ALTER TABLE compras.Requerimiento DROP CONSTRAINT ' + QUOTENAME(@constraintName) + N';';
    EXEC(@dropConstraintSql);
END;

-- Se conservan todos los valores preexistentes: Registrado, ValidadoAlmacen,
-- EnviadoOC, GeneradoOC y Anulado. Los tres nuevos estados no transforman filas.
ALTER TABLE compras.Requerimiento ADD CONSTRAINT CK_Requerimiento_Estado
CHECK (Estado IN (
    N'Registrado',
    N'EnviadoAlmacen',
    N'ValidadoAlmacen',
    N'AprobadoCoordinador',
    N'Rechazado',
    N'EnviadoOC',
    N'GeneradoOC',
    N'Anulado'
));

COMMIT TRANSACTION;
