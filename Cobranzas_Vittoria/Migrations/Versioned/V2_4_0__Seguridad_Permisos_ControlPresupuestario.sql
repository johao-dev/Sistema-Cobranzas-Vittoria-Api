-- =============================================
-- Version:     2.4.0
-- Description: Registra los permisos del modulo Control Presupuestario y los
--              asigna a los roles existentes.
--
--              Matriz aplicada (provisional, pendiente de validacion funcional):
--                Administrador -> todos los permisos.
--                contable      -> ver, administrar maestros, crear/editar
--                                 presupuesto, gestionar versiones, ajustes y
--                                 reportes. NO aprueba versiones.
--                ingeniero, Residente, Coordinador, Comprador -> ver y reportes.
--                Almacenero    -> ver.
--
--              La aprobacion de versiones queda solo en Administrador porque el
--              sistema todavia no tiene un rol Gerencia. Cuando se cree, debe
--              asignarsele control_presupuestario.aprobar_version y evaluarse si
--              se retira de Administrador.
--
--              Reentrante: cada INSERT valida su propia ausencia.
-- =============================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Permisos TABLE
(
    Codigo NVARCHAR(128) NOT NULL PRIMARY KEY,
    Nombre NVARCHAR(64) NOT NULL,
    Descripcion NVARCHAR(255) NOT NULL
);

INSERT INTO @Permisos (Codigo, Nombre, Descripcion) VALUES
    (N'control_presupuestario.ver',
     N'Ver control presupuestario',
     N'Consultar centros de costo, partidas, presupuestos, versiones y movimientos.'),
    (N'control_presupuestario.administrar_maestros',
     N'Administrar maestros presupuestales',
     N'Crear y editar centros de costo y el catalogo de partidas.'),
    (N'control_presupuestario.crear_presupuesto',
     N'Crear presupuesto',
     N'Crear presupuestos por centro de costo con su version inicial en borrador.'),
    (N'control_presupuestario.editar_presupuesto',
     N'Editar presupuesto',
     N'Editar la cabecera del presupuesto y asignar montos por partida en borrador.'),
    (N'control_presupuestario.gestionar_versiones',
     N'Gestionar versiones de presupuesto',
     N'Crear nuevas versiones a partir del snapshot vigente y anular borradores.'),
    (N'control_presupuestario.aprobar_version',
     N'Aprobar version de presupuesto',
     N'Aprobar una version para que su snapshot empiece a afectar saldos.'),
    (N'control_presupuestario.registrar_ajuste',
     N'Registrar ajuste presupuestal',
     N'Registrar movimientos de ajuste para corregir el ledger sin borrar historia.'),
    (N'control_presupuestario.ver_reportes',
     N'Ver reportes presupuestarios',
     N'Consultar presupuesto vs comprometido vs ejecutado, saldos y gastos.');

INSERT INTO seguridad.Permiso
    (Codigo, Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion)
SELECT p.Codigo, p.Nombre, p.Descripcion, 1, SYSUTCDATETIME(), N'migracion-rbac'
FROM @Permisos p
WHERE NOT EXISTS (
    SELECT 1 FROM seguridad.Permiso e WHERE e.Codigo = p.Codigo
);

DECLARE @Matriz TABLE
(
    NombreRol NVARCHAR(64) NOT NULL,
    CodigoPermiso NVARCHAR(128) NOT NULL,
    PRIMARY KEY (NombreRol, CodigoPermiso)
);

-- Administrador: acceso completo al modulo.
INSERT INTO @Matriz (NombreRol, CodigoPermiso)
SELECT N'Administrador', Codigo FROM @Permisos;

-- contable: opera el modulo de punta a punta salvo la aprobacion.
INSERT INTO @Matriz (NombreRol, CodigoPermiso)
SELECT N'contable', Codigo FROM @Permisos
WHERE Codigo <> N'control_presupuestario.aprobar_version';

-- Responsables de proyecto o area y Compras: consulta y reportes.
-- Compras necesita ver el saldo disponible antes de imputar una compra.
INSERT INTO @Matriz (NombreRol, CodigoPermiso)
SELECT r.Nombre, v.Codigo
FROM (VALUES (N'ingeniero'), (N'Residente'), (N'Coordinador'), (N'Comprador')) r(Nombre)
CROSS JOIN (VALUES
    (N'control_presupuestario.ver'),
    (N'control_presupuestario.ver_reportes')) v(Codigo);

-- Almacenero: solo consulta, para verificar saldo antes de mover stock.
INSERT INTO @Matriz (NombreRol, CodigoPermiso) VALUES
    (N'Almacenero', N'control_presupuestario.ver');

INSERT INTO seguridad.PermisoRol
    (IdPermiso, IdRol, FechaCreacion, UsuarioCreacion)
SELECT p.IdPermiso, r.IdRol, SYSUTCDATETIME(), N'migracion-rbac'
FROM @Matriz m
INNER JOIN seguridad.Permiso p ON p.Codigo = m.CodigoPermiso
INNER JOIN seguridad.Rol r ON r.Nombre = m.NombreRol
WHERE NOT EXISTS (
    SELECT 1
    FROM seguridad.PermisoRol pr
    WHERE pr.IdPermiso = p.IdPermiso
      AND pr.IdRol = r.IdRol
);

COMMIT TRANSACTION;
