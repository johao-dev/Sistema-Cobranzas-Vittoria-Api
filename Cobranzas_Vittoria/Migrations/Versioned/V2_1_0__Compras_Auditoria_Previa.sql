-- Guardar estos logs como evidencia histórica antes de DROP/ALTER.
SET NOCOUNT ON;
DECLARE @json NVARCHAR(MAX), @fragmento NVARCHAR(2000), @pos INT;
PRINT N'AUDITORIA_COMPRAS: Dependencias_SQL';
SELECT @json = (
    SELECT DISTINCT OBJECT_SCHEMA_NAME(d.referencing_id) AS SchemaConsumidor,
        OBJECT_NAME(d.referencing_id) AS ObjetoConsumidor, o.type_desc AS Tipo,
        OBJECT_SCHEMA_NAME(d.referenced_id) AS SchemaOrigen,
        OBJECT_NAME(d.referenced_id) AS ObjetoOrigen,
        COL_NAME(d.referenced_id,d.referenced_minor_id) AS ColumnaOrigen
    FROM sys.sql_expression_dependencies d JOIN sys.objects o ON o.object_id=d.referencing_id
    WHERE d.referenced_id IN (OBJECT_ID('compras.Requerimiento'),OBJECT_ID('compras.OrdenCompra'),
        OBJECT_ID('compras.Compra'),OBJECT_ID('ControlPresupuestario.Moneda'))
    FOR JSON PATH);
SET @pos = 1;
WHILE @pos <= LEN(@json)
BEGIN
 SET @fragmento = SUBSTRING(@json, @pos, 2000);
 RAISERROR(N'%s', 10, 1, @fragmento) WITH NOWAIT;
 SET @pos += 2000;
END;
PRINT N'AUDITORIA_COMPRAS: Proyecto_OC';
SELECT @json = (SELECT oc.IdOrdenCompra, oc.IdProyecto AS ProyectoCabecera, r.IdProyecto AS ProyectoRequerimiento
 FROM compras.OrdenCompra oc JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
 WHERE oc.IdProyecto <> r.IdProyecto FOR JSON PATH);
SET @pos = 1;
WHILE @pos <= LEN(@json)
BEGIN
 SET @fragmento = SUBSTRING(@json, @pos, 2000);
 RAISERROR(N'%s', 10, 1, @fragmento) WITH NOWAIT;
 SET @pos += 2000;
END;
PRINT N'AUDITORIA_COMPRAS: Proveedor_OC';
SELECT @json = (SELECT oc.IdOrdenCompra, oc.IdProveedor AS ProveedorCabecera, d.IdOrdenCompraDetalle, d.IdProveedor AS ProveedorDetalle
 FROM compras.OrdenCompra oc JOIN compras.OrdenCompraDetalle d ON d.IdOrdenCompra = oc.IdOrdenCompra
 WHERE oc.IdProveedor <> d.IdProveedor FOR JSON PATH);
SET @pos = 1;
WHILE @pos <= LEN(@json)
BEGIN
 SET @fragmento = SUBSTRING(@json, @pos, 2000);
 RAISERROR(N'%s', 10, 1, @fragmento) WITH NOWAIT;
 SET @pos += 2000;
END;
PRINT N'AUDITORIA_COMPRAS: Proveedor_Compra';
SELECT @json = (SELECT c.IdCompra, c.IdProveedor AS ProveedorCabecera, cd.IdMaterial, od.IdProveedor AS ProveedorDetalle
 FROM compras.Compra c JOIN compras.CompraDetalle cd ON cd.IdCompra = c.IdCompra
 LEFT JOIN compras.OrdenCompraDetalle od ON od.IdOrdenCompra = c.IdOrdenCompra AND od.IdMaterial = cd.IdMaterial
 WHERE od.IdProveedor IS NULL OR c.IdProveedor <> od.IdProveedor FOR JSON PATH);
SET @pos = 1;
WHILE @pos <= LEN(@json)
BEGIN
 SET @fragmento = SUBSTRING(@json, @pos, 2000);
 RAISERROR(N'%s', 10, 1, @fragmento) WITH NOWAIT;
 SET @pos += 2000;
END;
PRINT N'AUDITORIA_COMPRAS: Especialidad_Requerimiento';
SELECT @json = (SELECT r.IdRequerimiento, r.IdEspecialidad AS EspecialidadCabecera, rd.IdMaterial, m.IdEspecialidad AS EspecialidadMaterial
 FROM compras.Requerimiento r JOIN compras.RequerimientoDetalle rd ON rd.IdRequerimiento = r.IdRequerimiento
 JOIN maestra.Material m ON m.IdMaterial = rd.IdMaterial WHERE r.IdEspecialidad <> m.IdEspecialidad FOR JSON PATH);
SET @pos = 1;
WHILE @pos <= LEN(@json)
BEGIN
 SET @fragmento = SUBSTRING(@json, @pos, 2000);
 RAISERROR(N'%s', 10, 1, @fragmento) WITH NOWAIT;
 SET @pos += 2000;
END;
PRINT N'AUDITORIA_COMPRAS: Partida_sin_detalles';
SELECT @json = (SELECT r.IdRequerimiento, r.IdPresupuestoDetalle FROM compras.Requerimiento r
 WHERE r.IdPresupuestoDetalle IS NOT NULL AND NOT EXISTS
 (SELECT 1 FROM compras.RequerimientoDetalle rd WHERE rd.IdRequerimiento = r.IdRequerimiento) FOR JSON PATH);
SET @pos = 1;
WHILE @pos <= LEN(@json)
BEGIN
 SET @fragmento = SUBSTRING(@json, @pos, 2000);
 RAISERROR(N'%s', 10, 1, @fragmento) WITH NOWAIT;
 SET @pos += 2000;
END;
PRINT N'AUDITORIA_COMPRAS: Duplicados_OC';
SELECT @json = (SELECT IdOrdenCompra, IdMaterial, COUNT(*) AS Lineas FROM compras.OrdenCompraDetalle
 GROUP BY IdOrdenCompra, IdMaterial HAVING COUNT(*) > 1 FOR JSON PATH);
SET @pos = 1;
WHILE @pos <= LEN(@json)
BEGIN
 SET @fragmento = SUBSTRING(@json, @pos, 2000);
 RAISERROR(N'%s', 10, 1, @fragmento) WITH NOWAIT;
 SET @pos += 2000;
END;
PRINT N'AUDITORIA_COMPRAS: Duplicados_Compra';
SELECT @json = (SELECT IdCompra, IdMaterial, COUNT(*) AS Lineas FROM compras.CompraDetalle
 GROUP BY IdCompra, IdMaterial HAVING COUNT(*) > 1 FOR JSON PATH);
SET @pos = 1;
WHILE @pos <= LEN(@json)
BEGIN
 SET @fragmento = SUBSTRING(@json, @pos, 2000);
 RAISERROR(N'%s', 10, 1, @fragmento) WITH NOWAIT;
 SET @pos += 2000;
END;
PRINT N'AUDITORIA_COMPRAS: Precision_OC';
SELECT @json = (SELECT IdOrdenCompraDetalle, Subtotal FROM compras.OrdenCompraDetalle
 WHERE TRY_CONVERT(DECIMAL(18,2), Subtotal) IS NULL OR Subtotal <> ROUND(Subtotal,2) FOR JSON PATH);
SET @pos = 1;
WHILE @pos <= LEN(@json)
BEGIN
 SET @fragmento = SUBSTRING(@json, @pos, 2000);
 RAISERROR(N'%s', 10, 1, @fragmento) WITH NOWAIT;
 SET @pos += 2000;
END;
PRINT N'AUDITORIA_COMPRAS: Precision_Compra';
SELECT @json = (SELECT IdCompraDetalle, Subtotal FROM compras.CompraDetalle
 WHERE TRY_CONVERT(DECIMAL(18,2), Subtotal) IS NULL OR Subtotal <> ROUND(Subtotal,2) FOR JSON PATH);
SET @pos = 1;
WHILE @pos <= LEN(@json)
BEGIN
 SET @fragmento = SUBSTRING(@json, @pos, 2000);
 RAISERROR(N'%s', 10, 1, @fragmento) WITH NOWAIT;
 SET @pos += 2000;
END;
