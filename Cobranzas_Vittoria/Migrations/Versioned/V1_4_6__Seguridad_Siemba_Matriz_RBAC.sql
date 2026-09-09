-- =============================================
-- Author:      Johao Bravo
-- Version:     1.4.6
-- Create date: 2026-09-08
-- Description: Migración para la implementación de la matriz RBAC en el sistema de seguridad.
-- =============================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Renombrar el rol sin alterar su IdRol ni sus asociaciones existentes.
UPDATE seguridad.Rol
SET Nombre = N'Almacenero',
    FechaModificacion = SYSUTCDATETIME(),
    UsuarioModificacion = N'migracion-rbac'
WHERE Nombre = N'almacen';

-- Roles de la matriz inicial. Los roles existentes no cubiertos por esta
-- matriz (Contable e Ingeniero) se preservan sin nuevos permisos.
INSERT INTO seguridad.Rol (Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion)
SELECT valores.Nombre, valores.Descripcion, 1, SYSUTCDATETIME(), N'migracion-rbac'
FROM (VALUES
    (N'Residente', N'Crea y gestiona sus requerimientos antes del proceso de almacén.'),
    (N'Coordinador', N'Revisa requerimientos procesados y los envía a compras.'),
    (N'Comprador', N'Consulta requerimientos disponibles para el proceso de compra.')
) valores(Nombre, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM seguridad.Rol r WHERE r.Nombre = valores.Nombre);

-- Catálogo de permisos base.
INSERT INTO seguridad.Permiso (Codigo, Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion)
SELECT valores.Codigo, valores.Nombre, valores.Descripcion, 1, SYSUTCDATETIME(), N'migracion-rbac'
FROM (VALUES
    (N'requerimientos.ver', N'Ver requerimientos', N'Consultar requerimientos.'),
    (N'requerimientos.crear', N'Crear requerimiento', N'Crear requerimientos de materiales.'),
    (N'requerimientos.editar_borrador', N'Editar requerimiento en borrador', N'Editar requerimientos registrados antes de su procesamiento.'),
    (N'requerimientos.enviar', N'Enviar requerimiento', N'Enviar un requerimiento para procesamiento.'),
    (N'requerimientos.procesar_stock', N'Procesar requerimiento por stock', N'Registrar la validación de almacén.'),
    (N'requerimientos.ver_depuracion_almacen', N'Ver depuración de almacén', N'Consultar la información de depuración de almacén.'),
    (N'requerimientos.aprobar', N'Aprobar requerimiento', N'Dar visto bueno a un requerimiento.'),
    (N'requerimientos.rechazar', N'Rechazar requerimiento', N'Rechazar un requerimiento.'),
    (N'requerimientos.enviar_compras', N'Enviar requerimiento a compras', N'Enviar un requerimiento aprobado al proceso de compras.')
) valores(Codigo, Nombre, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM seguridad.Permiso p WHERE p.Codigo = valores.Codigo);

-- Matriz rol-permiso inicial. Administrador recibe explícitamente todo el
-- catálogo actual; no se aplica un bypass de código para ese rol.
DECLARE @Matriz TABLE (NombreRol nvarchar(100) NOT NULL, CodigoPermiso nvarchar(128) NOT NULL);
INSERT INTO @Matriz (NombreRol, CodigoPermiso) VALUES
    (N'Residente', N'requerimientos.ver'),
    (N'Residente', N'requerimientos.crear'),
    (N'Residente', N'requerimientos.editar_borrador'),
    (N'Residente', N'requerimientos.enviar'),
    (N'Almacenero', N'requerimientos.ver'),
    (N'Almacenero', N'requerimientos.procesar_stock'),
    (N'Almacenero', N'requerimientos.ver_depuracion_almacen'),
    (N'Coordinador', N'requerimientos.ver'),
    (N'Coordinador', N'requerimientos.ver_depuracion_almacen'),
    (N'Coordinador', N'requerimientos.aprobar'),
    (N'Coordinador', N'requerimientos.rechazar'),
    (N'Coordinador', N'requerimientos.enviar_compras'),
    (N'Comprador', N'requerimientos.ver'),
    (N'Comprador', N'requerimientos.ver_depuracion_almacen');

INSERT INTO @Matriz (NombreRol, CodigoPermiso)
SELECT N'Administrador', p.Codigo
FROM seguridad.Permiso p
WHERE p.Codigo LIKE N'requerimientos.%';

INSERT INTO seguridad.PermisoRol (IdPermiso, IdRol, FechaCreacion, UsuarioCreacion)
SELECT p.IdPermiso, r.IdRol, SYSUTCDATETIME(), N'migracion-rbac'
FROM @Matriz matriz
INNER JOIN seguridad.Rol r ON r.Nombre = matriz.NombreRol
INNER JOIN seguridad.Permiso p ON p.Codigo = matriz.CodigoPermiso
WHERE NOT EXISTS (
    SELECT 1
    FROM seguridad.PermisoRol existente
    WHERE existente.IdPermiso = p.IdPermiso
      AND existente.IdRol = r.IdRol
);

COMMIT TRANSACTION;
