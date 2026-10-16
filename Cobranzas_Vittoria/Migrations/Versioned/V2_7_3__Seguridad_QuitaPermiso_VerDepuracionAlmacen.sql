/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.7.3
Módulo      : Seguridad
Descripción : Quita el permiso no usado de ver depuración de almacén.
===============================================================================

El permiso de 'ver_depuracion_almacen' fue agregado en la versión 1.4.6, pero
nunca fue usado en la aplicación, por lo que se elimina de la matriz de permisos.

===============================================================================
*/

-- Eliminar sus relaciones con los roles.
DELETE FROM seguridad.PermisoRol
WHERE IdPermiso IN (
    SELECT p.IdPermiso
    FROM seguridad.Permiso p
    WHERE p.Codigo = N'requerimientos.ver_depuracion_almacen'
);

-- Eliminar el permiso.
DELETE FROM seguridad.Permiso
WHERE Codigo = N'requerimientos.ver_depuracion_almacen';