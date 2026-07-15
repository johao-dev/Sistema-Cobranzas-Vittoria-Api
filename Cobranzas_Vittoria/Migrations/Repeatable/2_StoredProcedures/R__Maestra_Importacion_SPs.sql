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
--   50001 - CAMPO_OBLIGATORIO            (campo obligatorio vacio o solo espacios)
--   50002 - VALOR_DUPLICADO_EN_ARCHIVO   (mismo Codigo/Ruc/RazonSocial/Nombre en 2+ filas)
--   50003 - VALOR_YA_EXISTE_EN_BD        (Codigo/Ruc/RazonSocial/Nombre ya registrado)
--   50004 - FK_NO_EXISTE                 (Foreign Key invalida o inexistente)
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


-- -----------------------------------------------------------------------------
-- Especialidad
-- Tabla destino: maestra.Especialidad (Nombre, Descripcion, Activo, FechaCreacion)
-- Validaciones:
--   - 50001: Nombre obligatorio.
--   - 50002: Nombres duplicados dentro del mismo archivo.
--   - 50003: Nombres que ya existen en BD (no hay UNIQUE explicita pero la
--            convencion de negocio exige unicidad de Nombre).
-- NOTA: la tabla no tiene UNIQUE en Nombre; usamos UPDLOCK,HOLDLOCK para
--       serializar la verificacion contra inserts concurrentes.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [maestra].[usp_Especialidad_CargaMasiva]
    @Filas maestra.TVP_Especialidad READONLY,
    @Usuario VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Validacion 1: obligatoriedad de Nombre
        IF EXISTS (
            SELECT 1 FROM @Filas
            WHERE NULLIF(LTRIM(RTRIM(Nombre)), '') IS NULL
        )
            THROW 50001, 'CAMPO_OBLIGATORIO: Nombre es requerido.', 1;

        -- Validacion 2: Nombres duplicados dentro del archivo
        IF EXISTS (
            SELECT Nombre FROM @Filas
            GROUP BY Nombre
            HAVING COUNT(*) > 1
        )
            THROW 50002, 'VALOR_DUPLICADO_EN_ARCHIVO: Nombre de Especialidad repetido en el archivo.', 1;

        -- Validacion 3: Nombres que ya existen en BD
        IF EXISTS (
            SELECT 1
            FROM @Filas f
            INNER JOIN maestra.Especialidad e WITH (UPDLOCK, HOLDLOCK) ON e.Nombre = f.Nombre
        )
            THROW 50003, 'VALOR_YA_EXISTE_EN_BD: Ya existe una Especialidad con ese Nombre.', 1;

        -- Insert
        DECLARE @RowCount INT = 0;
        INSERT INTO maestra.Especialidad (Nombre, Descripcion, Activo, FechaCreacion)
        SELECT Nombre, Descripcion, Activo, GETDATE()
        FROM @Filas;
        SET @RowCount = @@ROWCOUNT;

        COMMIT;
        SELECT @RowCount AS FilasInsertadas;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- Material
-- Tabla destino: maestra.Material
--   (IdEspecialidad, Codigo, Descripcion, UnidadMedida, StockMinimo, Activo,
--    FechaCreacion, IdUnidadMedida, CodigoProveedor)
-- Validaciones:
--   - 50001: IdEspecialidad, Descripcion, UnidadMedida obligatorios.
--   - 50002: Codigos duplicados dentro del archivo (Codigo es NULLable: solo
--            se valida si viene con valor).
--   - 50003: Codigos que ya existen en BD (idem).
--   - 50004: IdEspecialidad o IdUnidadMedida no existen en sus tablas.
-- NOTA: el SP autogenera Codigo si llega NULL (sigue patron de usp_Material_Upsert
--       en R__Maestra_SPs.sql: 'MAT-' + correlativo).
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [maestra].[usp_Material_CargaMasiva]
    @Filas maestra.TVP_Material READONLY,
    @Usuario VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Validacion 1: obligatoriedad
        IF EXISTS (
            SELECT 1 FROM @Filas
            WHERE IdEspecialidad IS NULL
               OR NULLIF(LTRIM(RTRIM(Descripcion)), '') IS NULL
               OR NULLIF(LTRIM(RTRIM(UnidadMedida)), '') IS NULL
        )
            THROW 50001, 'CAMPO_OBLIGATORIO: IdEspecialidad, Descripcion y UnidadMedida son requeridos.', 1;

        -- Validacion 2: Codigos duplicados dentro del archivo (solo si vienen con valor)
        IF EXISTS (
            SELECT Codigo FROM @Filas
            WHERE NULLIF(LTRIM(RTRIM(Codigo)), '') IS NOT NULL
            GROUP BY Codigo
            HAVING COUNT(*) > 1
        )
            THROW 50002, 'VALOR_DUPLICADO_EN_ARCHIVO: Codigo de Material repetido en el archivo.', 1;

        -- Validacion 3: Codigos que ya existen en BD
        IF EXISTS (
            SELECT 1
            FROM @Filas f
            INNER JOIN maestra.Material m WITH (UPDLOCK, HOLDLOCK) ON m.Codigo = f.Codigo
            WHERE NULLIF(LTRIM(RTRIM(f.Codigo)), '') IS NOT NULL
        )
            THROW 50003, 'VALOR_YA_EXISTE_EN_BD: Ya existe un Material con ese Codigo.', 1;

        -- Validacion 4: FKs existentes
        IF EXISTS (
            SELECT 1 FROM @Filas f
            WHERE NOT EXISTS (SELECT 1 FROM maestra.Especialidad e WHERE e.IdEspecialidad = f.IdEspecialidad)
        )
            THROW 50004, 'FK_NO_EXISTE: Alguna fila referencia un IdEspecialidad inexistente.', 1;

        IF EXISTS (
            SELECT 1 FROM @Filas f
            WHERE f.IdUnidadMedida IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM maestra.UnidadMedida u WHERE u.IdUnidadMedida = f.IdUnidadMedida)
        )
            THROW 50004, 'FK_NO_EXISTE: Alguna fila referencia un IdUnidadMedida inexistente.', 1;

        -- Insert con autogeneracion de Codigo cuando viene NULL.
        -- Estrategia: si el Codigo es NULL, lo generamos como 'MAT-' + siguiente correlativo
        -- dentro del mismo batch (ROW_NUMBER particionado) + el maximo actual en la tabla.
        DECLARE @MaxActual INT = ISNULL(
            (SELECT MAX(TRY_CONVERT(INT, REPLACE(Codigo, 'MAT-', ''))) FROM maestra.Material WHERE Codigo LIKE 'MAT-[0-9]%'),
            0
        );
        DECLARE @RowCount INT = 0;
        INSERT INTO maestra.Material
            (IdEspecialidad, Codigo, Descripcion, UnidadMedida, StockMinimo, Activo, FechaCreacion, IdUnidadMedida, CodigoProveedor)
        SELECT
            f.IdEspecialidad,
            ISNULL(NULLIF(LTRIM(RTRIM(f.Codigo)), ''),
                   CONCAT('MAT-', RIGHT(CONCAT('0000', @MaxActual + ROW_NUMBER() OVER (ORDER BY f._Fila)), 4))),
            f.Descripcion,
            f.UnidadMedida,
            ISNULL(f.StockMinimo, 0),
            ISNULL(f.Activo, 1),
            GETDATE(),
            f.IdUnidadMedida,
            NULLIF(LTRIM(RTRIM(f.CodigoProveedor)), '')
        FROM @Filas f;
        SET @RowCount = @@ROWCOUNT;

        COMMIT;
        SELECT @RowCount AS FilasInsertadas;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- Proveedor
-- Tabla destino: maestra.Proveedor (RazonSocial, Ruc, ..., Activo, FechaCreacion)
-- Validaciones:
--   - 50001: RazonSocial y Ruc obligatorios.
--   - 50002: Rucs duplicados dentro del archivo.
--   - 50003: Rucs que ya existen en BD.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [maestra].[usp_Proveedor_CargaMasiva]
    @Filas maestra.TVP_Proveedor READONLY,
    @Usuario VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Validacion 1: obligatoriedad
        IF EXISTS (
            SELECT 1 FROM @Filas
            WHERE NULLIF(LTRIM(RTRIM(RazonSocial)), '') IS NULL
               OR NULLIF(LTRIM(RTRIM(Ruc)), '') IS NULL
        )
            THROW 50001, 'CAMPO_OBLIGATORIO: RazonSocial y Ruc son requeridos.', 1;

        -- Validacion 2: Rucs duplicados dentro del archivo
        IF EXISTS (
            SELECT Ruc FROM @Filas
            GROUP BY Ruc
            HAVING COUNT(*) > 1
        )
            THROW 50002, 'VALOR_DUPLICADO_EN_ARCHIVO: Ruc repetido en el archivo.', 1;

        -- Validacion 3: Rucs que ya existen en BD
        IF EXISTS (
            SELECT 1
            FROM @Filas f
            INNER JOIN maestra.Proveedor p WITH (UPDLOCK, HOLDLOCK) ON p.Ruc = f.Ruc
        )
            THROW 50003, 'VALOR_YA_EXISTE_EN_BD: Ya existe un Proveedor con ese Ruc.', 1;

        -- Insert
        DECLARE @RowCount INT = 0;
        INSERT INTO maestra.Proveedor
            (RazonSocial, Ruc, Contacto, Telefono, Correo, Direccion, Banco, CuentaCorriente, CCI,
             CuentaDetraccion, DescripcionServicio, Observacion, TrabajamosConProveedor, Activo, FechaCreacion)
        SELECT
            RazonSocial, Ruc, Contacto, Telefono, Correo, Direccion, Banco, CuentaCorriente, CCI,
            CuentaDetraccion, DescripcionServicio, Observacion, TrabajamosConProveedor, ISNULL(Activo, 1), GETDATE()
        FROM @Filas;
        SET @RowCount = @@ROWCOUNT;

        COMMIT;
        SELECT @RowCount AS FilasInsertadas;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- ProveedorGastoAdministrativo
-- Tabla destino: maestra.ProveedorGastoAdministrativo
--   (RazonSocial UNIQUE, Ruc UNIQUE si no es NULL, IdCategoriaGasto FK opcional)
-- Validaciones:
--   - 50001: RazonSocial obligatorio.
--   - 50002: RazonSocial duplicado intra-archivo.
--   - 50003: RazonSocial o Ruc ya existentes en BD.
--   - 50004: IdCategoriaGasto no existe.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [maestra].[usp_ProveedorGastoAdministrativo_CargaMasiva]
    @Filas maestra.TVP_ProveedorGastoAdministrativo READONLY,
    @Usuario VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Validacion 1: obligatoriedad
        IF EXISTS (
            SELECT 1 FROM @Filas
            WHERE NULLIF(LTRIM(RTRIM(RazonSocial)), '') IS NULL
        )
            THROW 50001, 'CAMPO_OBLIGATORIO: RazonSocial es requerida.', 1;

        -- Validacion 2: RazonSocial duplicada intra-archivo
        IF EXISTS (
            SELECT RazonSocial FROM @Filas
            GROUP BY RazonSocial
            HAVING COUNT(*) > 1
        )
            THROW 50002, 'VALOR_DUPLICADO_EN_ARCHIVO: RazonSocial repetida en el archivo.', 1;

        -- Validacion 3: RazonSocial ya existe en BD (indice UNIQUE)
        IF EXISTS (
            SELECT 1
            FROM @Filas f
            INNER JOIN maestra.ProveedorGastoAdministrativo p WITH (UPDLOCK, HOLDLOCK) ON p.RazonSocial = f.RazonSocial
        )
            THROW 50003, 'VALOR_YA_EXISTE_EN_BD: Ya existe un ProveedorGastoAdministrativo con esa RazonSocial.', 1;

        -- Validacion 3b: Ruc ya existe en BD (solo si la fila trae Ruc)
        IF EXISTS (
            SELECT 1
            FROM @Filas f
            INNER JOIN maestra.ProveedorGastoAdministrativo p WITH (UPDLOCK, HOLDLOCK) ON p.Ruc = f.Ruc
            WHERE NULLIF(LTRIM(RTRIM(f.Ruc)), '') IS NOT NULL
        )
            THROW 50003, 'VALOR_YA_EXISTE_EN_BD: Ya existe un ProveedorGastoAdministrativo con ese Ruc.', 1;

        -- Validacion 4: IdCategoriaGasto debe existir
        IF EXISTS (
            SELECT 1 FROM @Filas f
            WHERE f.IdCategoriaGasto IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM maestra.CategoriaGasto c WHERE c.IdCategoriaGasto = f.IdCategoriaGasto)
        )
            THROW 50004, 'FK_NO_EXISTE: Alguna fila referencia un IdCategoriaGasto inexistente.', 1;

        -- Insert
        DECLARE @RowCount INT = 0;
        INSERT INTO maestra.ProveedorGastoAdministrativo
            (RazonSocial, Ruc, Contacto, Telefono, Correo, Activo, FechaCreacion, IdCategoriaGasto)
        SELECT
            RazonSocial, Ruc, Contacto, Telefono, Correo, ISNULL(Activo, 1), GETDATE(), IdCategoriaGasto
        FROM @Filas;
        SET @RowCount = @@ROWCOUNT;

        COMMIT;
        SELECT @RowCount AS FilasInsertadas;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- ProveedorTerreno
-- Tabla destino: maestra.ProveedorTerreno
--   (RazonSocial tiene indice nonclustered, unicidad por convencion de negocio)
-- Validaciones:
--   - 50001: RazonSocial obligatorio.
--   - 50002: RazonSocial duplicada intra-archivo.
--   - 50003: RazonSocial ya existe en BD.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [maestra].[usp_ProveedorTerreno_CargaMasiva]
    @Filas maestra.TVP_ProveedorTerreno READONLY,
    @Usuario VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Validacion 1: obligatoriedad
        IF EXISTS (
            SELECT 1 FROM @Filas
            WHERE NULLIF(LTRIM(RTRIM(RazonSocial)), '') IS NULL
        )
            THROW 50001, 'CAMPO_OBLIGATORIO: RazonSocial es requerida.', 1;

        -- Validacion 2: RazonSocial duplicada intra-archivo
        IF EXISTS (
            SELECT RazonSocial FROM @Filas
            GROUP BY RazonSocial
            HAVING COUNT(*) > 1
        )
            THROW 50002, 'VALOR_DUPLICADO_EN_ARCHIVO: RazonSocial repetida en el archivo.', 1;

        -- Validacion 3: RazonSocial ya existe en BD
        IF EXISTS (
            SELECT 1
            FROM @Filas f
            INNER JOIN maestra.ProveedorTerreno p WITH (UPDLOCK, HOLDLOCK) ON p.RazonSocial = f.RazonSocial
        )
            THROW 50003, 'VALOR_YA_EXISTE_EN_BD: Ya existe un ProveedorTerreno con esa RazonSocial.', 1;

        -- Insert
        DECLARE @RowCount INT = 0;
        INSERT INTO maestra.ProveedorTerreno
            (RazonSocial, Ruc, Contacto, Telefono, Correo, Activo, FechaCreacion)
        SELECT
            RazonSocial, Ruc, Contacto, Telefono, Correo, ISNULL(Activo, 1), GETDATE()
        FROM @Filas;
        SET @RowCount = @@ROWCOUNT;

        COMMIT;
        SELECT @RowCount AS FilasInsertadas;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- CategoriaGasto
-- Tabla destino: maestra.CategoriaGasto (Nombre, Activo, FechaCreacion)
-- Validaciones:
--   - 50001: Nombre obligatorio.
--   - 50002: Nombre duplicado intra-archivo.
--   - 50003: Nombre ya existe en BD (no hay UNIQUE explicita pero la
--            convencion de negocio exige unicidad de Nombre).
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [maestra].[usp_CategoriaGasto_CargaMasiva]
    @Filas maestra.TVP_CategoriaGasto READONLY,
    @Usuario VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Validacion 1: obligatoriedad
        IF EXISTS (
            SELECT 1 FROM @Filas
            WHERE NULLIF(LTRIM(RTRIM(Nombre)), '') IS NULL
        )
            THROW 50001, 'CAMPO_OBLIGATORIO: Nombre es requerido.', 1;

        -- Validacion 2: Nombres duplicados dentro del archivo
        IF EXISTS (
            SELECT Nombre FROM @Filas
            GROUP BY Nombre
            HAVING COUNT(*) > 1
        )
            THROW 50002, 'VALOR_DUPLICADO_EN_ARCHIVO: Nombre de CategoriaGasto repetido en el archivo.', 1;

        -- Validacion 3: Nombres que ya existen en BD
        IF EXISTS (
            SELECT 1
            FROM @Filas f
            INNER JOIN maestra.CategoriaGasto c WITH (UPDLOCK, HOLDLOCK) ON c.Nombre = f.Nombre
        )
            THROW 50003, 'VALOR_YA_EXISTE_EN_BD: Ya existe una CategoriaGasto con ese Nombre.', 1;

        -- Insert
        DECLARE @RowCount INT = 0;
        INSERT INTO maestra.CategoriaGasto (Nombre, Activo, FechaCreacion)
        SELECT Nombre, ISNULL(Activo, 1), GETDATE()
        FROM @Filas;
        SET @RowCount = @@ROWCOUNT;

        COMMIT;
        SELECT @RowCount AS FilasInsertadas;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO
