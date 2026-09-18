-- Fase expandible y reintentable. Dejar IdMoneda nullable permite corregir el backfill
-- documentado si existen OC históricas. DbUp NO registra esta migración hasta completar NOT NULL.
IF COL_LENGTH('compras.OrdenCompra','IdMoneda') IS NULL
    ALTER TABLE compras.OrdenCompra ADD IdMoneda INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('compras.OrdenCompra') AND name='FK_OrdenCompra_Moneda')
    ALTER TABLE compras.OrdenCompra WITH CHECK ADD CONSTRAINT FK_OrdenCompra_Moneda
    FOREIGN KEY (IdMoneda) REFERENCES maestra.Moneda(IdMoneda);
GO
IF EXISTS (SELECT 1 FROM compras.OrdenCompra WHERE IdMoneda IS NULL)
BEGIN
    DECLARE @pendientes NVARCHAR(MAX), @fragmento NVARCHAR(2000), @pos INT=1;
    SELECT @pendientes=(SELECT IdOrdenCompra,NumeroOrdenCompra,IdRequerimiento FROM compras.OrdenCompra WHERE IdMoneda IS NULL FOR JSON PATH);
    PRINT N'BACKFILL_MONEDA_OC_PENDIENTE: validar moneda documental por OC; no asumir PEN ni la moneda del presupuesto.';
    WHILE @pos<=LEN(@pendientes)
    BEGIN
        SET @fragmento=SUBSTRING(@pendientes,@pos,2000);
        RAISERROR(N'%s',10,1,@fragmento) WITH NOWAIT;
        SET @pos+=2000;
    END;
END;
GO
-- Separar la evidencia JSON del THROW evita que DbUp trate sus llaves como formato
-- al registrar el SqlException del siguiente lote.
IF EXISTS (SELECT 1 FROM compras.OrdenCompra WHERE IdMoneda IS NULL)
    THROW 51340, 'Moneda histórica de OC sin determinar. Completar IdMoneda con evidencia y reintentar DbUp; se conserva la columna nullable.', 1;
ALTER TABLE compras.OrdenCompra ALTER COLUMN IdMoneda INT NOT NULL;
