-- La nueva capacidad amplía la anterior: se asigna explícitamente a los roles estándar.
-- Se conserva editar_cantidades_almacen hasta retirar su endpoint y migrar el frontend.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
    IF EXISTS (SELECT 1 FROM compras.RequerimientoDetalle WITH (TABLOCKX, HOLDLOCK) WHERE Cantidad <= 0)
        THROW 51350, 'Existen cantidades no positivas en RequerimientoDetalle. Corregirlas antes de migrar; no se eliminan datos.', 1;
    IF EXISTS (SELECT IdRequerimiento, IdMaterial FROM compras.RequerimientoDetalle
        GROUP BY IdRequerimiento, IdMaterial HAVING COUNT(*) > 1)
        THROW 51351, 'Existen materiales duplicados en RequerimientoDetalle. Conciliarlos antes de migrar; no se eliminan datos.', 1;
    ALTER TABLE compras.RequerimientoDetalle WITH CHECK
        ADD CONSTRAINT CK_RequerimientoDetalle_CantidadPositiva CHECK (Cantidad > 0);
    ALTER TABLE compras.RequerimientoDetalle
        ADD CONSTRAINT UQ_RequerimientoDetalle_Requerimiento_Material UNIQUE (IdRequerimiento, IdMaterial);

    INSERT INTO seguridad.Permiso (Codigo, Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion)
    SELECT N'requerimientos.gestionar_items_almacen', N'Gestionar ítems del requerimiento en almacén',
        N'Agregar, actualizar y eliminar ítems en estado EnviadoAlmacen.', 1, SYSUTCDATETIME(), N'migracion-rbac'
    WHERE NOT EXISTS (SELECT 1 FROM seguridad.Permiso WHERE Codigo = N'requerimientos.gestionar_items_almacen');
    INSERT INTO seguridad.PermisoRol (IdPermiso, IdRol, FechaCreacion, UsuarioCreacion)
    SELECT p.IdPermiso, r.IdRol, SYSUTCDATETIME(), N'migracion-rbac'
    FROM seguridad.Permiso p CROSS JOIN seguridad.Rol r
    WHERE p.Codigo = N'requerimientos.gestionar_items_almacen'
        AND r.Nombre IN (N'Almacenero', N'Administrador')
        AND NOT EXISTS (SELECT 1 FROM seguridad.PermisoRol pr WHERE pr.IdPermiso = p.IdPermiso AND pr.IdRol = r.IdRol);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
