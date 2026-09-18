SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
 IF EXISTS (SELECT IdOrdenCompra,IdMaterial FROM compras.OrdenCompraDetalle WITH (TABLOCKX,HOLDLOCK)
     GROUP BY IdOrdenCompra,IdMaterial HAVING COUNT(*)>1)
     THROW 51330, 'Hay materiales duplicados en OrdenCompraDetalle: revisar AUDITORIA_COMPRAS. No se eliminarán líneas.', 1;
 IF NOT EXISTS (
     SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID('compras.OrdenCompraDetalle')
     AND i.is_unique=1 AND i.has_filter=0 AND i.is_disabled=0
     AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=2
     AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
         WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0 AND c.name='IdOrdenCompra')
     AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
         WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0 AND c.name='IdMaterial'))
     ALTER TABLE compras.OrdenCompraDetalle ADD CONSTRAINT UQ_OrdenCompraDetalle_OrdenCompra_Material UNIQUE (IdOrdenCompra,IdMaterial);
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
