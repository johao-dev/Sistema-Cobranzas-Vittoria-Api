-- =============================================================================
-- Repeatable: R__Maestra_Importacion_SPs.sql
--
-- Stored procedures para carga masiva de entidades de maestra.
-- Patron: el SP recibe un TVP con los datos, valida y persiste en una sola
-- transaccion. Si cualquier fila falla, se hace rollback completo y se
-- relanza el error como SqlException.
--
-- El servicio (ImportService) captura el SqlException y lo traduce a
-- DatosInvalidosException (HTTP 422) con el detalle por fila.
--
-- Codigos de error usados (THROW):
--   50001 - CODIGO_NOMBRE_OBLIGATORIO   (campo obligatorio vacio)
--   50002 - CODIGO_DUPLICADO_EN_ARCHIVO (mismo Codigo en varias filas)
--   50003 - CODIGO_YA_EXISTE_EN_BD      (Codigo ya registrado)
--
-- @Usuario: el repository siempre lo envia (para mantener uniforme el contrato
-- de IImportRepository). Si la tabla no tiene columna UsuarioCreacion, el SP
-- lo declara pero no lo usa; si la tiene, lo usa en el INSERT.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- UnidadMedida
-- Tabla destino: maestra.UnidadMedida (Codigo, Nombre, Activo, FechaCreacion)
-- NOTA: esta tabla NO tiene columna UsuarioCreacion, por lo que @Usuario se
--       recibe pero no se persiste.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [maestra].[usp_UnidadMedida_CargaMasiva]
    @Filas maestra.TVP_UnidadMedida READONLY,
    @Usuario VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Validacion 1: obligatoriedad de Codigo y Nombre (incluye vacios y solo espacios)
        IF EXISTS (
            SELECT 1 FROM @Filas
            WHERE NULLIF(LTRIM(RTRIM(Codigo)), '') IS NULL
               OR NULLIF(LTRIM(RTRIM(Nombre)), '') IS NULL
        )
            THROW 50001, 'CODIGO_NOMBRE_OBLIGATORIO', 1;

        -- Validacion 2: Codigos duplicados dentro del mismo archivo
        IF EXISTS (
            SELECT Codigo FROM @Filas
            GROUP BY Codigo
            HAVING COUNT(*) > 1
        )
            THROW 50002, 'CODIGO_DUPLICADO_EN_ARCHIVO', 1;

        -- Validacion 3: Codigos que ya existen en la BD (el feature es solo INSERT,
        -- no UPSERT; si el usuario quiere actualizar, debe usar el endpoint normal)
        IF EXISTS (
            SELECT 1
            FROM @Filas f
            INNER JOIN maestra.UnidadMedida u WITH (UPDLOCK, HOLDLOCK) ON u.Codigo = f.Codigo
        )
            THROW 50003, 'CODIGO_YA_EXISTE_EN_BD', 1;

        -- Insert
        DECLARE @RowCount INT = 0;
        INSERT INTO maestra.UnidadMedida (Codigo, Nombre, Activo, FechaCreacion)
        SELECT Codigo, Nombre, Activo, GETDATE()
        FROM @Filas;
        SET @RowCount = @@ROWCOUNT;

        COMMIT;

        -- Devolvemos el conteo como un result set escalar. Usamos SELECT en lugar
        -- de RETURN porque Dapper.ExecuteAsync sobre un SP con SET NOCOUNT ON +
        -- BEGIN TRAN no siempre propaga el valor de RETURN al cliente.
        SELECT @RowCount AS FilasInsertadas;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO
