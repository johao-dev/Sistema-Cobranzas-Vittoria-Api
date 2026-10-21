-- La especialidad es una propiedad del material. No se corrigen ni consolidan
-- datos ambiguos: la auditoría debe pasar antes de modificar la estructura.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    -- Evitar escrituras entre la auditoría y los ALTER de esta migración.
    DECLARE @Filas BIGINT;
    SELECT @Filas = COUNT_BIG(*) FROM maestra.Material WITH (TABLOCKX, HOLDLOCK);
    SELECT @Filas = COUNT_BIG(*) FROM almacen.KardexEntrada WITH (TABLOCKX, HOLDLOCK);
    SELECT @Filas = COUNT_BIG(*) FROM almacen.KardexSalida WITH (TABLOCKX, HOLDLOCK);
    SELECT @Filas = COUNT_BIG(*) FROM almacen.KardexSalidaDetalle WITH (TABLOCKX, HOLDLOCK);
    SELECT @Filas = COUNT_BIG(*) FROM almacen.KardexStock WITH (TABLOCKX, HOLDLOCK);

    IF EXISTS (SELECT IdMaterial FROM almacen.KardexStock GROUP BY IdMaterial HAVING COUNT_BIG(*) > 1)
        THROW 51180, 'AUDITORIA_INVENTARIO: hay varias filas de stock por material. Conciliar antes de reintentar; no se consolidan automáticamente.', 1;

    IF EXISTS (
        SELECT 1 FROM almacen.KardexEntrada ke
        JOIN maestra.Material m ON m.IdMaterial = ke.IdMaterial
        WHERE ke.IdEspecialidad <> m.IdEspecialidad
    ) OR EXISTS (
        SELECT 1 FROM almacen.KardexStock ks
        JOIN maestra.Material m ON m.IdMaterial = ks.IdMaterial
        WHERE ks.IdEspecialidad <> m.IdEspecialidad
    ) OR EXISTS (
        SELECT 1 FROM almacen.KardexSalida ks
        JOIN almacen.KardexSalidaDetalle d ON d.IdKardexSalida = ks.IdKardexSalida
        JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
        WHERE ks.IdEspecialidad <> m.IdEspecialidad
    )
        THROW 51181, 'AUDITORIA_INVENTARIO: la especialidad guardada difiere de la del material. Resolver la clasificación antes de reintentar.', 1;

    DECLARE @Descuadres BIGINT;
    ;WITH Entradas AS (
        SELECT IdMaterial, CAST(SUM(Cantidad) AS DECIMAL(28,2)) AS Cantidad
        FROM almacen.KardexEntrada GROUP BY IdMaterial
    ), Salidas AS (
        SELECT IdMaterial, CAST(SUM(Cantidad) AS DECIMAL(28,2)) AS Cantidad
        FROM almacen.KardexSalidaDetalle GROUP BY IdMaterial
    ), Movimientos AS (
        SELECT COALESCE(e.IdMaterial, s.IdMaterial) AS IdMaterial,
            ISNULL(e.Cantidad, 0) AS Entrada, ISNULL(s.Cantidad, 0) AS Salida
        FROM Entradas e FULL JOIN Salidas s ON s.IdMaterial = e.IdMaterial
    )
    SELECT @Descuadres = COUNT_BIG(*)
    FROM Movimientos m FULL JOIN almacen.KardexStock ks ON ks.IdMaterial = m.IdMaterial
    WHERE ISNULL(ks.TotalEntrada, 0) <> ISNULL(m.Entrada, 0)
       OR ISNULL(ks.TotalSalida, 0) <> ISNULL(m.Salida, 0)
       OR ISNULL(ks.Stock, 0) <> ISNULL(m.Entrada, 0) - ISNULL(m.Salida, 0);

    IF @Descuadres > 0
        THROW 51182, 'AUDITORIA_INVENTARIO: los acumulados de stock no coinciden con los movimientos. Conciliar antes de reintentar.', 1;

    IF EXISTS (
        SELECT 1 FROM almacen.KardexSalida s
        WHERE NOT EXISTS (SELECT 1 FROM almacen.KardexSalidaDetalle d WHERE d.IdKardexSalida = s.IdKardexSalida)
    )
        THROW 51183, 'AUDITORIA_INVENTARIO: existen salidas sin detalles. Resolver antes de reintentar.', 1;

    ALTER TABLE almacen.KardexEntrada DROP CONSTRAINT FK_KardexEntrada_Especialidad;
    DROP INDEX IX_KardexEntrada_Material_Especialidad ON almacen.KardexEntrada;
    ALTER TABLE almacen.KardexEntrada DROP COLUMN IdEspecialidad;
    CREATE INDEX IX_KardexEntrada_Material ON almacen.KardexEntrada (IdMaterial);

    ALTER TABLE almacen.KardexSalida DROP CONSTRAINT FK_KardexSalida_Especialidad;
    ALTER TABLE almacen.KardexSalida DROP COLUMN IdEspecialidad;

    ALTER TABLE almacen.KardexStock DROP CONSTRAINT FK_KardexStock_Especialidad;
    ALTER TABLE almacen.KardexStock DROP CONSTRAINT UQ_KardexStock_Material_Especialidad;
    ALTER TABLE almacen.KardexStock DROP COLUMN IdEspecialidad;
    ALTER TABLE almacen.KardexStock ADD CONSTRAINT UQ_KardexStock_Material UNIQUE (IdMaterial);

    PRINT 'AUDITORIA_INVENTARIO: completada. Se conservan movimientos, identificadores y acumulados; stock único por material.';
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
