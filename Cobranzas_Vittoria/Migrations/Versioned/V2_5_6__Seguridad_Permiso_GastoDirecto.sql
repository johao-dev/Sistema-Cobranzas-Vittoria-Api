-- =============================================
-- Version:     2.5.6
-- Description: Permiso para operar gastos directos (registrar, editar,
--              confirmar, anular y adjuntar documentos) desde las secciones
--              de Operaciones → Gastos del proyecto.
--
--              Hasta ahora los endpoints de gasto directo no exigían ni sesión
--              (hallazgo H-01). La consulta usa control_presupuestario.ver.
--
--              Matriz: Administrador y contable operan; el resto solo consulta.
--
--              Reentrante: cada INSERT valida su propia ausencia.
-- =============================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM seguridad.Permiso WHERE Codigo = N'gasto_directo.operar')
    INSERT INTO seguridad.Permiso (Codigo, Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion)
    VALUES (N'gasto_directo.operar', N'Operar gastos directos',
        N'Registrar, editar, confirmar y anular gastos directos y adjuntar sus documentos.',
        1, SYSUTCDATETIME(), N'migracion-rbac');

INSERT INTO seguridad.PermisoRol (IdPermiso, IdRol, FechaCreacion, UsuarioCreacion)
SELECT p.IdPermiso, r.IdRol, SYSUTCDATETIME(), N'migracion-rbac'
FROM seguridad.Permiso p
CROSS JOIN seguridad.Rol r
WHERE p.Codigo = N'gasto_directo.operar'
  AND r.Nombre IN (N'Administrador', N'contable')
  AND NOT EXISTS (SELECT 1 FROM seguridad.PermisoRol pr WHERE pr.IdPermiso = p.IdPermiso AND pr.IdRol = r.IdRol);

COMMIT TRANSACTION;
