SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
 IF EXISTS (SELECT IdCompra,IdMaterial FROM compras.CompraDetalle WITH (TABLOCKX,HOLDLOCK)
     GROUP BY IdCompra,IdMaterial HAVING COUNT(*)>1)
     THROW 51330, 'Hay materiales duplicados en CompraDetalle: revisar AUDITORIA_COMPRAS. No se eliminarán líneas.', 1;
 IF NOT EXISTS (
     SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID('compras.CompraDetalle')
     AND i.is_unique=1 AND i.has_filter=0 AND i.is_disabled=0
     AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=2
     AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
         WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0 AND c.name='IdCompra')
     AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
         WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0 AND c.name='IdMaterial'))
     ALTER TABLE compras.CompraDetalle ADD CONSTRAINT UQ_CompraDetalle_Compra_Material UNIQUE (IdCompra,IdMaterial);
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
