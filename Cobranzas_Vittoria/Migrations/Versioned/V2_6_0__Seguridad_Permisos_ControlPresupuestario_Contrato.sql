-- =============================================
-- Version:     2.6.0
-- Description: Permisos por capacidad de negocio del contrato de API de Control
--              Presupuestario (sección 13) y retiro de los permisos genéricos de V2.4.0.
--
--              Cada rol recibe los permisos nuevos equivalentes a los que ya tenía,
--              así nadie gana ni pierde acceso:
--                ver                  -> centro_costo.ver, partida.ver, presupuesto.ver,
--                                        movimiento.ver y gasto_directo.ver
--                administrar_maestros -> centro_costo.crear/actualizar, partida.crear/actualizar
--                crear_presupuesto    -> presupuesto.crear
--                editar_presupuesto   -> presupuesto.actualizar, presupuesto.editar_detalle
--                gestionar_versiones  -> version.crear, version.anular
--                aprobar_version      -> version.aprobar
--                registrar_ajuste     -> presupuesto.registrar_ajuste
--                ver_reportes         -> reporte.ver
--
--              gasto_directo.ver reemplaza a control_presupuestario.ver en la consulta de
--              gastos directos. Los usuarios deben volver a iniciar sesión: los permisos
--              viajan dentro del JWT.
--
--              Reentrante: los INSERT validan su ausencia y los DELETE solo tocan los
--              códigos retirados.
-- =============================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Nuevos TABLE
(
    Codigo NVARCHAR(128) NOT NULL PRIMARY KEY,
    Nombre NVARCHAR(64) NOT NULL,
    Descripcion NVARCHAR(255) NOT NULL
);

INSERT INTO @Nuevos (Codigo, Nombre, Descripcion) VALUES
    (N'control_presupuestario.centro_costo.ver', N'Ver centros de costo', N'Consultar centros de costo.'),
    (N'control_presupuestario.centro_costo.crear', N'Crear centros de costo', N'Crear e importar centros de costo.'),
    (N'control_presupuestario.centro_costo.actualizar', N'Actualizar centros de costo', N'Editar y desactivar centros de costo.'),
    (N'control_presupuestario.partida.ver', N'Ver catálogo de partidas', N'Consultar el catálogo de partidas.'),
    (N'control_presupuestario.partida.crear', N'Crear partidas', N'Crear e importar partidas del catálogo.'),
    (N'control_presupuestario.partida.actualizar', N'Actualizar partidas', N'Editar, mover y desactivar partidas del catálogo.'),
    (N'control_presupuestario.presupuesto.ver', N'Ver presupuestos', N'Consultar presupuestos, versiones y partidas de versión.'),
    (N'control_presupuestario.presupuesto.crear', N'Crear presupuestos', N'Crear presupuestos con su versión inicial en borrador.'),
    (N'control_presupuestario.presupuesto.actualizar', N'Actualizar presupuestos', N'Editar la cabecera de un presupuesto.'),
    (N'control_presupuestario.presupuesto.editar_detalle', N'Editar partidas de versión',
     N'Agregar, editar, eliminar, cargar e importar montos de una versión en borrador.'),
    (N'control_presupuestario.presupuesto.registrar_ajuste', N'Registrar ajustes presupuestales',
     N'Registrar ajustes manuales del ledger sin borrar historia.'),
    (N'control_presupuestario.version.crear', N'Crear versiones', N'Crear una versión nueva a partir del snapshot vigente.'),
    (N'control_presupuestario.version.aprobar', N'Aprobar versiones', N'Aprobar una versión para que afecte saldos.'),
    (N'control_presupuestario.version.anular', N'Anular versiones', N'Anular una versión en borrador.'),
    (N'control_presupuestario.movimiento.ver', N'Ver movimientos presupuestales', N'Consultar el ledger de una partida.'),
    (N'control_presupuestario.reporte.ver', N'Ver reportes presupuestarios',
     N'Consultar resumen, saldos, presupuesto vs comprometido y ejecutado, gastos y dashboard.'),
    (N'gasto_directo.ver', N'Ver gastos directos', N'Consultar gastos directos y sus documentos.');

INSERT INTO seguridad.Permiso (Codigo, Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion)
SELECT n.Codigo, n.Nombre, n.Descripcion, 1, SYSUTCDATETIME(), N'migracion-rbac'
FROM @Nuevos n
WHERE NOT EXISTS (SELECT 1 FROM seguridad.Permiso p WHERE p.Codigo = n.Codigo);

DECLARE @Equivalencia TABLE
(
    Viejo NVARCHAR(128) NOT NULL,
    Nuevo NVARCHAR(128) NOT NULL,
    PRIMARY KEY (Viejo, Nuevo)
);

INSERT INTO @Equivalencia (Viejo, Nuevo) VALUES
    (N'control_presupuestario.ver', N'control_presupuestario.centro_costo.ver'),
    (N'control_presupuestario.ver', N'control_presupuestario.partida.ver'),
    (N'control_presupuestario.ver', N'control_presupuestario.presupuesto.ver'),
    (N'control_presupuestario.ver', N'control_presupuestario.movimiento.ver'),
    (N'control_presupuestario.ver', N'gasto_directo.ver'),
    (N'control_presupuestario.administrar_maestros', N'control_presupuestario.centro_costo.crear'),
    (N'control_presupuestario.administrar_maestros', N'control_presupuestario.centro_costo.actualizar'),
    (N'control_presupuestario.administrar_maestros', N'control_presupuestario.partida.crear'),
    (N'control_presupuestario.administrar_maestros', N'control_presupuestario.partida.actualizar'),
    (N'control_presupuestario.crear_presupuesto', N'control_presupuestario.presupuesto.crear'),
    (N'control_presupuestario.editar_presupuesto', N'control_presupuestario.presupuesto.actualizar'),
    (N'control_presupuestario.editar_presupuesto', N'control_presupuestario.presupuesto.editar_detalle'),
    (N'control_presupuestario.gestionar_versiones', N'control_presupuestario.version.crear'),
    (N'control_presupuestario.gestionar_versiones', N'control_presupuestario.version.anular'),
    (N'control_presupuestario.aprobar_version', N'control_presupuestario.version.aprobar'),
    (N'control_presupuestario.registrar_ajuste', N'control_presupuestario.presupuesto.registrar_ajuste'),
    (N'control_presupuestario.ver_reportes', N'control_presupuestario.reporte.ver');

INSERT INTO seguridad.PermisoRol (IdPermiso, IdRol, FechaCreacion, UsuarioCreacion)
SELECT DISTINCT nuevo.IdPermiso, pr.IdRol, SYSUTCDATETIME(), N'migracion-rbac'
FROM seguridad.PermisoRol pr
INNER JOIN seguridad.Permiso viejo ON viejo.IdPermiso = pr.IdPermiso
INNER JOIN @Equivalencia e ON e.Viejo = viejo.Codigo
INNER JOIN seguridad.Permiso nuevo ON nuevo.Codigo = e.Nuevo
WHERE NOT EXISTS (
    SELECT 1 FROM seguridad.PermisoRol x WHERE x.IdPermiso = nuevo.IdPermiso AND x.IdRol = pr.IdRol
);

-- Administrador conserva el acceso completo al módulo aunque le faltara algún permiso viejo.
INSERT INTO seguridad.PermisoRol (IdPermiso, IdRol, FechaCreacion, UsuarioCreacion)
SELECT p.IdPermiso, r.IdRol, SYSUTCDATETIME(), N'migracion-rbac'
FROM seguridad.Permiso p
CROSS JOIN seguridad.Rol r
WHERE r.Nombre = N'Administrador'
  AND p.Codigo IN (SELECT Codigo FROM @Nuevos)
  AND NOT EXISTS (SELECT 1 FROM seguridad.PermisoRol x WHERE x.IdPermiso = p.IdPermiso AND x.IdRol = r.IdRol);

-- Retiro de los permisos genéricos, ya reemplazados.
DELETE pr
FROM seguridad.PermisoRol pr
INNER JOIN seguridad.Permiso p ON p.IdPermiso = pr.IdPermiso
WHERE p.Codigo IN (SELECT DISTINCT Viejo FROM @Equivalencia);

DELETE FROM seguridad.Permiso
WHERE Codigo IN (SELECT DISTINCT Viejo FROM @Equivalencia);

COMMIT TRANSACTION;
