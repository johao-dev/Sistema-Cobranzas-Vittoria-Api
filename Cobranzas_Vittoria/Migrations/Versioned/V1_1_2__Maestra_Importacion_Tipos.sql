-- =============================================================================
-- Migracion versionada: V1_1_2__Maestra_Importacion_Tipos.sql
--
-- Crea los User-Defined Table Types (TVPs) necesarios para los SPs de carga
-- masiva de entidades de maestra.
--
-- Convenciones:
--   - Nombre: TVP_<Entidad> (sigue el patron del TVP existente en V1_0_7)
--   - Columnas: las del DTO de importacion (mismas que la entidad destino)
--               + _Fila INT NOT NULL (numero de fila del archivo, para reportar
--               errores con contexto).
--   - Las columnas usan nvarchar (no varchar) para que el DataTable de ADO.NET
--     (que es Unicode por defecto) se mapee sin conversion implicita.
-- =============================================================================

CREATE TYPE maestra.TVP_UnidadMedida AS TABLE (
    Codigo        NVARCHAR(20)  NOT NULL,
    Nombre        NVARCHAR(100) NOT NULL,
    Activo        BIT           NOT NULL,
    _Fila         INT           NOT NULL
);
GO
