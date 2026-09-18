SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
 IF EXISTS (SELECT 1 FROM compras.OrdenCompraDetalle WHERE TRY_CONVERT(DECIMAL(18,2),Subtotal) IS NULL OR Subtotal<>ROUND(Subtotal,2))
     OR EXISTS (SELECT 1 FROM compras.CompraDetalle WHERE TRY_CONVERT(DECIMAL(18,2),Subtotal) IS NULL OR Subtotal<>ROUND(Subtotal,2))
     THROW 51320, 'Subtotales incompatibles con DECIMAL(18,2): revisar AUDITORIA_COMPRAS. No se redondearán datos históricos.', 1;
 -- El resto de cantidades, precios y snapshots está versionado como DECIMAL(18,2).
 -- Detectar desviaciones en la DB real en lugar de alterarlas sin una auditoría específica.
 IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
     JOIN sys.schemas s ON s.schema_id=t.schema_id
     WHERE s.name='compras' AND t.name IN ('RequerimientoDetalle','OrdenCompra','OrdenCompraDetalle','Compra','CompraDetalle')
     AND c.name IN ('Cantidad','PrecioUnitario','Total','SubtotalSinIGV','MontoIGV','MontoTotal')
     AND (TYPE_NAME(c.system_type_id) NOT IN ('decimal','numeric') OR c.precision<>18 OR c.scale<>2))
     THROW 51321, 'Precisión de columnas diferente al modelo versionado: auditar datos antes de ALTER COLUMN.', 1;
 ALTER TABLE compras.OrdenCompraDetalle DROP COLUMN Subtotal;
 ALTER TABLE compras.OrdenCompraDetalle ADD Subtotal AS (CONVERT(DECIMAL(18,2),ROUND(Cantidad*PrecioUnitario,2))) PERSISTED;
 ALTER TABLE compras.CompraDetalle DROP COLUMN Subtotal;
 ALTER TABLE compras.CompraDetalle ADD Subtotal AS (CONVERT(DECIMAL(18,2),ROUND(Cantidad*PrecioUnitario,2))) PERSISTED;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
