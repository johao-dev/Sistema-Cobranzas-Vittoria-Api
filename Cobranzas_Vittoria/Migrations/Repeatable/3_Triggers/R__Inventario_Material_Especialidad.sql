-- La restricción cubre la API, importaciones y escrituras SQL directas.
-- Los SP de Inventario mantienen un bloqueo de lectura del material hasta
-- terminar la transacción, para coordinar altas de movimientos con este UPDATE.
CREATE OR ALTER TRIGGER maestra.TR_Material_Especialidad_ConMovimientos
ON maestra.Material
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF UPDATE(IdEspecialidad) AND EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.IdMaterial = i.IdMaterial
        WHERE i.IdEspecialidad <> d.IdEspecialidad
          AND (
              EXISTS (SELECT 1 FROM almacen.KardexEntrada e WHERE e.IdMaterial = i.IdMaterial)
              OR EXISTS (SELECT 1 FROM almacen.KardexSalidaDetalle s WHERE s.IdMaterial = i.IdMaterial)
              OR EXISTS (SELECT 1 FROM almacen.KardexStock st WHERE st.IdMaterial = i.IdMaterial)
              OR EXISTS (SELECT 1 FROM almacen.KardexMovimiento km WHERE km.IdMaterial = i.IdMaterial)
          )
    )
        THROW 51112, 'ESPECIALIDAD_MATERIAL_CON_MOVIMIENTOS: No se puede cambiar la especialidad de un material con movimientos de inventario.', 1;
END;
GO
