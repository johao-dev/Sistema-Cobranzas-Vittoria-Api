-- El catalogo de permisos se mantiene por migraciones y ya no admite altas ni bajas por API.
-- Quitar sus definiciones del script repetible no elimina los SP de bases existentes.
DROP PROCEDURE IF EXISTS seguridad.usp_Permiso_GetByCodigo;
DROP PROCEDURE IF EXISTS seguridad.usp_Permiso_Insert;
DROP PROCEDURE IF EXISTS seguridad.usp_Permiso_Delete;
