/*
  Hace de maestra.Proveedor la identidad canónica de los proveedores contables.
  El mapping se conserva: es evidencia de cómo se reconciliaron IDs de secuencias
  IDENTITY independientes y permite migrar gastos/documentos sin comparar IDs.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS
(
    SELECT 1
    FROM maestra.Proveedor
    WHERE NULLIF(LTRIM(RTRIM(Ruc)), '') IS NOT NULL
    GROUP BY LTRIM(RTRIM(Ruc))
    HAVING COUNT(*) > 1
)
BEGIN
    DECLARE @Duplicados NVARCHAR(MAX) =
    (
        SELECT LTRIM(RTRIM(Ruc)) AS Ruc, COUNT(*) AS Cantidad
        FROM maestra.Proveedor
        WHERE NULLIF(LTRIM(RTRIM(Ruc)), '') IS NOT NULL
        GROUP BY LTRIM(RTRIM(Ruc))
        HAVING COUNT(*) > 1
        FOR JSON PATH
    );
    PRINT N'CONCILIACION_PROVEEDOR_RUC_DUPLICADO: ' + LEFT(@Duplicados, 3500);
    THROW 51550, 'Existen RUC no nulos duplicados en maestra.Proveedor. Conciliar antes de crear la unicidad física.', 1;
END;

-- La cadena vacía no es un documento. Los sentinelas de los catálogos legacy
-- se interpretan como ausencia solo durante la consolidación, sin reescribirlos.
UPDATE maestra.Proveedor SET Ruc = NULL WHERE NULLIF(LTRIM(RTRIM(Ruc)), '') IS NULL;

ALTER TABLE maestra.Proveedor ALTER COLUMN RazonSocial NVARCHAR(250) NOT NULL;
ALTER TABLE maestra.Proveedor ALTER COLUMN Telefono NVARCHAR(50) NULL;
ALTER TABLE maestra.Proveedor ALTER COLUMN Ruc NVARCHAR(20) NULL;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'maestra.Proveedor')
      AND name = N'UX_Proveedor_Ruc_NoNulo'
)
    CREATE UNIQUE INDEX UX_Proveedor_Ruc_NoNulo
        ON maestra.Proveedor(Ruc)
        WHERE Ruc IS NOT NULL;

CREATE TABLE maestra.ProveedorLegacyMap
(
    Origen VARCHAR(40) NOT NULL,
    IdLegacy INT NOT NULL,
    IdProveedor INT NOT NULL,
    Criterio VARCHAR(30) NOT NULL,
    RucNormalizado NVARCHAR(20) NULL,
    FechaMigracion DATETIME2(0) NOT NULL
        CONSTRAINT DF_ProveedorLegacyMap_FechaMigracion DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ProveedorLegacyMap PRIMARY KEY (Origen, IdLegacy),
    CONSTRAINT FK_ProveedorLegacyMap_Proveedor FOREIGN KEY (IdProveedor)
        REFERENCES maestra.Proveedor(IdProveedor),
    CONSTRAINT CK_ProveedorLegacyMap_Origen CHECK
        (Origen IN ('PROVEEDOR_TERRENO', 'PROVEEDOR_GASTO_ADMIN')),
    CONSTRAINT CK_ProveedorLegacyMap_Criterio CHECK
        (Criterio IN ('RUC_CONFIABLE', 'IDENTIDAD_NUEVA'))
);

DECLARE @Origen VARCHAR(40), @IdLegacy INT, @RazonSocial NVARCHAR(250),
    @Ruc NVARCHAR(20), @Contacto NVARCHAR(150), @Telefono NVARCHAR(50),
    @Correo NVARCHAR(150), @Activo BIT, @FechaCreacion DATETIME2(0),
    @RucConfiable NVARCHAR(20), @IdProveedor INT, @Criterio VARCHAR(30);

DECLARE proveedores CURSOR LOCAL FAST_FORWARD FOR
    SELECT 'PROVEEDOR_TERRENO', IdProveedorTerreno, RazonSocial, Ruc,
        Contacto, Telefono, Correo, Activo, CONVERT(DATETIME2(0), FechaCreacion)
    FROM maestra.ProveedorTerreno
    UNION ALL
    SELECT 'PROVEEDOR_GASTO_ADMIN', IdProveedorGastoAdministrativo, RazonSocial,
        Ruc, Contacto, Telefono, Correo, Activo, CONVERT(DATETIME2(0), FechaCreacion)
    FROM maestra.ProveedorGastoAdministrativo
    ORDER BY 1, 2;

OPEN proveedores;
FETCH NEXT FROM proveedores INTO @Origen, @IdLegacy, @RazonSocial, @Ruc,
    @Contacto, @Telefono, @Correo, @Activo, @FechaCreacion;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Ruc = NULLIF(LTRIM(RTRIM(@Ruc)), '');
    SET @RucConfiable = CASE
        WHEN LEN(@Ruc) = 11
         AND @Ruc NOT LIKE '%[^0-9]%'
         AND @Ruc <> REPLICATE('0', 11)
        THEN @Ruc ELSE NULL END;
    SET @IdProveedor = NULL;
    SET @Criterio = 'IDENTIDAD_NUEVA';

    IF @RucConfiable IS NOT NULL
    BEGIN
        SELECT @IdProveedor = IdProveedor
        FROM maestra.Proveedor WITH (UPDLOCK, HOLDLOCK)
        WHERE Ruc = @RucConfiable;
        IF @IdProveedor IS NOT NULL SET @Criterio = 'RUC_CONFIABLE';
    END;

    IF @IdProveedor IS NULL
    BEGIN
        INSERT INTO maestra.Proveedor
            (RazonSocial, Ruc, Contacto, Telefono, Correo, Activo, FechaCreacion)
        VALUES
            (@RazonSocial, @RucConfiable, @Contacto, @Telefono, @Correo,
             ISNULL(@Activo, 1), COALESCE(@FechaCreacion, SYSUTCDATETIME()));
        SET @IdProveedor = CONVERT(INT, SCOPE_IDENTITY());
    END;

    INSERT INTO maestra.ProveedorLegacyMap
        (Origen, IdLegacy, IdProveedor, Criterio, RucNormalizado)
    VALUES
        (@Origen, @IdLegacy, @IdProveedor, @Criterio, @RucConfiable);

    FETCH NEXT FROM proveedores INTO @Origen, @IdLegacy, @RazonSocial, @Ruc,
        @Contacto, @Telefono, @Correo, @Activo, @FechaCreacion;
END;
CLOSE proveedores;
DEALLOCATE proveedores;

-- Las tablas se conservan para reconciliación histórica, pero dejan de ser
-- catálogos escribibles: se retiran sus superficies SQL de mantenimiento.
DROP PROCEDURE IF EXISTS maestra.usp_ProveedorGastoAdministrativo_Delete;
DROP PROCEDURE IF EXISTS maestra.usp_ProveedorGastoAdministrativo_List;
DROP PROCEDURE IF EXISTS maestra.usp_ProveedorGastoAdministrativo_Upsert;
DROP PROCEDURE IF EXISTS maestra.usp_ProveedorGastoAdministrativo_CargaMasiva;
DROP PROCEDURE IF EXISTS maestra.usp_ProveedorTerreno_CargaMasiva;
IF TYPE_ID(N'maestra.TVP_ProveedorGastoAdministrativo') IS NOT NULL
    DROP TYPE maestra.TVP_ProveedorGastoAdministrativo;
IF TYPE_ID(N'maestra.TVP_ProveedorTerreno') IS NOT NULL
    DROP TYPE maestra.TVP_ProveedorTerreno;

-- El tipo original imponía RUC NOT NULL. Se recrea para que la carga masiva
-- respete el mismo contrato nullable que las altas individuales.
IF OBJECT_ID(N'maestra.usp_Proveedor_CargaMasiva', N'P') IS NOT NULL
    DROP PROCEDURE maestra.usp_Proveedor_CargaMasiva;
IF TYPE_ID(N'maestra.TVP_Proveedor') IS NOT NULL
    DROP TYPE maestra.TVP_Proveedor;
EXEC(N'CREATE TYPE maestra.TVP_Proveedor AS TABLE
(
    RazonSocial NVARCHAR(250) NOT NULL,
    Ruc NVARCHAR(20) NULL,
    Contacto NVARCHAR(150) NULL,
    Telefono NVARCHAR(50) NULL,
    Correo NVARCHAR(150) NULL,
    Direccion NVARCHAR(250) NULL,
    Banco NVARCHAR(50) NULL,
    CuentaCorriente NVARCHAR(50) NULL,
    CCI NVARCHAR(50) NULL,
    CuentaDetraccion NVARCHAR(50) NULL,
    DescripcionServicio NVARCHAR(250) NULL,
    Observacion NVARCHAR(250) NULL,
    TrabajamosConProveedor NVARCHAR(10) NULL,
    Activo BIT NULL,
    _Fila INT NOT NULL
);');

COMMIT TRANSACTION;
