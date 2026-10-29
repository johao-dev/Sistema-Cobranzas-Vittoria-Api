-- Limpieza administrativa opcional. DbUp NO ejecuta los scripts de Migrations/Manual.
-- Ejecutar en la base seleccionada después de revisar que estos cuatro backups
-- del reinicio del 29/10/2026 ya no se necesitan para recuperación o auditoría.
-- Los nombres son explícitos; no se buscan ni eliminan tablas por patrones.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @Backups TABLE (IdObjeto INT PRIMARY KEY);
    INSERT INTO @Backups
    SELECT object_id FROM sys.tables
    WHERE schema_id = SCHEMA_ID(N'almacen')
      AND name IN (
          N'KardexEntrada_BKP_20261029_PreReinicioStockProyecto',
          N'KardexSalida_BKP_20261029_PreReinicioStockProyecto',
          N'KardexSalidaDetalle_BKP_20261029_PreReinicioStockProyecto',
          N'KardexStock_BKP_20261029_PreReinicioStockProyecto');

    -- Detenerse si algún objeto persistente depende de estos backups.
    IF EXISTS (
        SELECT 1 FROM sys.sql_expression_dependencies d
        JOIN @Backups b ON b.IdObjeto = d.referenced_id
    ) OR EXISTS (
        SELECT 1 FROM sys.foreign_keys fk
        JOIN @Backups b ON b.IdObjeto = fk.referenced_object_id
    )
        THROW 50000, 'Los backups tienen dependencias. Revisarlas antes de eliminarlos.', 1;

    DROP TABLE IF EXISTS almacen.KardexSalidaDetalle_BKP_20261029_PreReinicioStockProyecto;
    DROP TABLE IF EXISTS almacen.KardexSalida_BKP_20261029_PreReinicioStockProyecto;
    DROP TABLE IF EXISTS almacen.KardexEntrada_BKP_20261029_PreReinicioStockProyecto;
    DROP TABLE IF EXISTS almacen.KardexStock_BKP_20261029_PreReinicioStockProyecto;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
GO
