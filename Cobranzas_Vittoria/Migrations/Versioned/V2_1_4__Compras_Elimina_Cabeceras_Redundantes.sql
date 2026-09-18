SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
 ALTER TABLE compras.Requerimiento DROP CONSTRAINT FK_Requerimiento_Especialidad;
 ALTER TABLE compras.Requerimiento DROP COLUMN IdEspecialidad;
 ALTER TABLE compras.OrdenCompra DROP CONSTRAINT FK_OrdenCompra_Proyecto, FK_OrdenCompra_Proveedor;
 ALTER TABLE compras.OrdenCompra DROP COLUMN IdProyecto, IdProveedor;
 ALTER TABLE compras.Compra DROP CONSTRAINT FK_Compra_Proveedor;
 ALTER TABLE compras.Compra DROP COLUMN IdProveedor;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
