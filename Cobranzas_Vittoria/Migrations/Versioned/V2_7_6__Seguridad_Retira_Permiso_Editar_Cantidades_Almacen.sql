-- El frontend utiliza la gestión de ítems y ya no consume el endpoint de cantidades.
-- Se retira la capacidad anterior sin trasladar sus asignaciones: gestionar ítems
-- permite también altas y bajas, por lo que concederla automáticamente ampliaría privilegios.
SET XACT_ABORT ON;

BEGIN TRANSACTION;
BEGIN TRY
    DELETE pr
    FROM seguridad.PermisoRol pr
    INNER JOIN seguridad.Permiso p ON p.IdPermiso = pr.IdPermiso
    WHERE p.Codigo = N'requerimientos.editar_cantidades_almacen';

    DELETE FROM seguridad.Permiso
    WHERE Codigo = N'requerimientos.editar_cantidades_almacen';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
