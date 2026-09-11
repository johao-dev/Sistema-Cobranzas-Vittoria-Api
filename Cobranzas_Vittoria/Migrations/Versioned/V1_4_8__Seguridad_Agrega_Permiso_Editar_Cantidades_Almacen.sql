-- =============================================
-- Version:     1.4.8
-- Description: Registra el permiso para ajustar cantidades y lo asigna a Almacenero y Administrador.
-- =============================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Codigo nvarchar(128) = N'requerimientos.editar_cantidades_almacen';

INSERT INTO seguridad.Permiso
    (Codigo, Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion)
SELECT
    @Codigo,
    N'Editar cantidades de requerimiento en almacén',
    N'Ajustar cantidades de detalles mientras el requerimiento está enviado a almacén.',
    1,
    SYSUTCDATETIME(),
    N'migracion-rbac'
WHERE NOT EXISTS (
    SELECT 1
    FROM seguridad.Permiso
    WHERE Codigo = @Codigo
);

INSERT INTO seguridad.PermisoRol
    (IdPermiso, IdRol, FechaCreacion, UsuarioCreacion)
SELECT p.IdPermiso, r.IdRol, SYSUTCDATETIME(), N'migracion-rbac'
FROM seguridad.Permiso p
INNER JOIN seguridad.Rol r ON r.Nombre IN (N'Almacenero', N'Administrador')
WHERE p.Codigo = @Codigo
  AND NOT EXISTS (
      SELECT 1
      FROM seguridad.PermisoRol pr
      WHERE pr.IdPermiso = p.IdPermiso
        AND pr.IdRol = r.IdRol
  );

COMMIT TRANSACTION;
