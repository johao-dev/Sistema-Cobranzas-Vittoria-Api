-- =============================================================================
-- Migracion versionada: V1_2_3__Maestra_Material_CargaMasiva_v1_Drop.sql
--
-- Limpieza de la version v1 del feature de importacion masiva de Materiales.
-- La v1 estaba compuesta por:
--   - SP:  maestra.usp_Material_CargaMasiva  (definido en R__Maestra_Importacion_SPs.sql)
--   - TVP: maestra.TVP_Material              (definido en V1_1_2)
--
-- El feature v2 (Fases 1-5 del diseno) reemplazo completamente a la v1:
--   - Nuevo SP  maestra.usp_Material_CargaMasiva_v2
--   - Nuevo TVP maestra.TVP_Material_v2
--   - El endpoint POST /api/import/material y la resolucion de catalogos
--     (ResolvedorEntidadesService) ya no invocan v1.
--
-- Por lo tanto v1 es codigo muerto: se elimina para evitar confusion y
-- mantener un unico path de importacion.
--
-- Orden de los DROP: primero el SP (porque depende del TVP), luego el TVP.
-- Ambos usan IF OBJECT_ID(...) IS NOT NULL para ser idempotentes.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 1. Drop del SP v1
-- -----------------------------------------------------------------------------
IF OBJECT_ID('[maestra].[usp_Material_CargaMasiva]', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE [maestra].[usp_Material_CargaMasiva];
    PRINT 'Dropped maestra.usp_Material_CargaMasiva (v1).';
END
ELSE
BEGIN
    PRINT 'maestra.usp_Material_CargaMasiva (v1) no existe; skip.';
END
GO


-- -----------------------------------------------------------------------------
-- 2. Drop del TVP v1
-- Solo se ejecuta si el SP v1 ya no existe (sino, fallaria por dependencia).
-- -----------------------------------------------------------------------------
IF OBJECT_ID('[maestra].[usp_Material_CargaMasiva]', 'P') IS NULL
   AND TYPE_ID('[maestra].[TVP_Material]') IS NOT NULL
BEGIN
    DROP TYPE [maestra].[TVP_Material];
    PRINT 'Dropped maestra.TVP_Material (v1).';
END
ELSE IF TYPE_ID('[maestra].[TVP_Material]') IS NOT NULL
BEGIN
    PRINT 'maestra.usp_Material_CargaMasiva (v1) aun existe; no se dropea TVP_Material.';
END
ELSE
BEGIN
    PRINT 'maestra.TVP_Material (v1) no existe; skip.';
END
GO
