CREATE OR ALTER PROCEDURE [maestra].[usp_Especialidad_List]
    @Activo BIT = NULL
AS
BEGIN
    SET
    NOCOUNT ON;
    
    SELECT
    IdEspecialidad,
    Nombre,
    Descripcion,
    Activo,
    FechaCreacion
FROM
    maestra.Especialidad
WHERE
    (@Activo IS NULL
        OR Activo = @Activo)
ORDER BY
    Nombre;
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_Especialidad_Upsert]
    @IdEspecialidad INT = NULL,
    @Nombre NVARCHAR(100),
    @Descripcion NVARCHAR(250) = NULL,
    @Activo BIT = 1
AS
BEGIN
    SET
NOCOUNT ON;

SET
XACT_ABORT ON;

IF NULLIF(LTRIM(RTRIM(@Nombre)), '') IS NULL
        THROW 50010,
'Nombre de especialidad es requerido.',
1;

IF @IdEspecialidad IS NULL
    BEGIN
        IF EXISTS (
SELECT
    1
FROM
    maestra.Especialidad
WHERE
    Nombre = @Nombre)
            THROW 50011,
'Ya existe la especialidad.',
1;

INSERT
    INTO
    maestra.Especialidad(Nombre,
    Descripcion,
    Activo)
VALUES (@Nombre,
@Descripcion,
@Activo);

SELECT
    CAST(SCOPE_IDENTITY() AS INT) AS IdEspecialidad;
END
ELSE
    BEGIN
        IF EXISTS (
SELECT
1
FROM
maestra.Especialidad
WHERE
Nombre = @Nombre
AND IdEspecialidad <> @IdEspecialidad)
            THROW 50012,
'Ya existe otra especialidad con ese nombre.',
1;

UPDATE
    maestra.Especialidad
SET
    Nombre = @Nombre,
               Descripcion = @Descripcion,
               Activo = @Activo
WHERE
    IdEspecialidad = @IdEspecialidad;

SELECT
    @IdEspecialidad AS IdEspecialidad;
END
END;
GO

CREATE OR ALTER PROCEDURE maestra.usp_Material_Get
    @IdMaterial INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
        m.IdMaterial,
        m.IdEspecialidad,
        e.Nombre AS Especialidad,
        m.Codigo,
        m.CodigoProveedor,
        m.Descripcion,
        m.UnidadMedida,
        m.StockMinimo,
        m.Activo,
        m.FechaCreacion
FROM
    maestra.Material m
INNER JOIN maestra.Especialidad e ON
    e.IdEspecialidad = m.IdEspecialidad
WHERE
    m.IdMaterial = @IdMaterial;
END;
GO

CREATE OR ALTER PROCEDURE maestra.usp_Material_List
    @Activo BIT = NULL,
    @IdEspecialidad INT = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
        m.IdMaterial,
        m.IdEspecialidad,
        e.Nombre AS Especialidad,
        m.Codigo,
        m.CodigoProveedor,
        m.Descripcion,
        m.UnidadMedida,
        m.StockMinimo,
        m.Activo,
        m.FechaCreacion
FROM
    maestra.Material m
INNER JOIN maestra.Especialidad e ON
    e.IdEspecialidad = m.IdEspecialidad
WHERE
    (@Activo IS NULL
        OR m.Activo = @Activo)
    AND (@IdEspecialidad IS NULL
        OR m.IdEspecialidad = @IdEspecialidad)
ORDER BY
    e.Nombre,
    m.Descripcion;
END;
GO

CREATE OR ALTER PROCEDURE maestra.usp_Material_Upsert
    @IdMaterial INT = NULL,
    @IdEspecialidad INT,
    @Codigo VARCHAR(50) = NULL,
    @CodigoProveedor VARCHAR(100) = NULL,
    @Descripcion VARCHAR(250),
    @UnidadMedida VARCHAR(50),
    @StockMinimo DECIMAL(18, 2) = 0,
    @Activo BIT = 1
AS
BEGIN
    SET
NOCOUNT ON;

SET
@Codigo = NULLIF(LTRIM(RTRIM(@Codigo)), '');

SET
@CodigoProveedor = NULLIF(LTRIM(RTRIM(@CodigoProveedor)), '');

IF @IdMaterial IS NULL
OR @IdMaterial = 0
    BEGIN
        IF @Codigo IS NULL
        BEGIN
            SELECT
    @Codigo = CONCAT('MAT-', RIGHT(CONCAT('0000', ISNULL(MAX(TRY_CONVERT(INT, REPLACE(Codigo, 'MAT-', ''))), 0) + 1), 4))
FROM
    maestra.Material
WHERE
    Codigo LIKE 'MAT-[0-9]%';
END;

INSERT
    INTO
    maestra.Material
        (
            IdEspecialidad,
            Codigo,
            CodigoProveedor,
            Descripcion,
            UnidadMedida,
            StockMinimo,
            Activo,
            FechaCreacion
        )
VALUES
        (
            @IdEspecialidad,
            @Codigo,
            @CodigoProveedor,
            @Descripcion,
            @UnidadMedida,
            ISNULL(@StockMinimo, 0),
            ISNULL(@Activo, 1),
            GETDATE()
        );

SELECT
    CONVERT(INT, SCOPE_IDENTITY()) AS IdMaterial;

RETURN;
END;

UPDATE
    maestra.Material
SET
        IdEspecialidad = @IdEspecialidad,
        Codigo = ISNULL(@Codigo, Codigo),
        CodigoProveedor = @CodigoProveedor,
        Descripcion = @Descripcion,
        UnidadMedida = @UnidadMedida,
        StockMinimo = ISNULL(@StockMinimo, 0),
        Activo = ISNULL(@Activo, 1)
WHERE
    IdMaterial = @IdMaterial;

SELECT
    @IdMaterial AS IdMaterial;
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_Proveedor_Get]
    @IdProveedor INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
    p.IdProveedor,
            p.RazonSocial,
            p.Ruc,
            p.Contacto,
            p.Telefono,
            p.Correo,
            p.Direccion,
            p.Banco,
            p.CuentaCorriente,
            p.CCI,
            p.CuentaDetraccion,
            p.DescripcionServicio,
            p.Observacion,
            p.TrabajamosConProveedor,
            p.Activo,
            p.FechaCreacion
FROM
    maestra.Proveedor p
WHERE
    p.IdProveedor = @IdProveedor;

SELECT
    pe.IdProveedorEspecialidad,
            pe.IdProveedor,
            pe.IdEspecialidad,
            e.Nombre AS Especialidad,
            pe.Activo
FROM
    maestra.ProveedorEspecialidad pe
INNER JOIN maestra.Especialidad e ON
    e.IdEspecialidad = pe.IdEspecialidad
WHERE
    pe.IdProveedor = @IdProveedor
ORDER BY
    e.Nombre;
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_Proveedor_List]
    @Activo BIT = NULL,
    @IdEspecialidad INT = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
    DISTINCT
           p.IdProveedor,
           p.RazonSocial,
           p.Ruc,
           p.Contacto,
           p.Telefono,
           p.Correo,
           p.Direccion,
           p.Banco,
           p.CuentaCorriente,
           p.CCI,
           p.CuentaDetraccion,
           p.DescripcionServicio,
           p.Observacion,
           p.TrabajamosConProveedor,
           p.Activo,
           p.FechaCreacion
FROM
    maestra.Proveedor p
LEFT JOIN maestra.ProveedorEspecialidad pe ON
    pe.IdProveedor = p.IdProveedor
    AND pe.Activo = 1
WHERE
    (@Activo IS NULL
        OR p.Activo = @Activo)
    AND (@IdEspecialidad IS NULL
        OR pe.IdEspecialidad = @IdEspecialidad)
ORDER BY
    p.RazonSocial;
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_Proveedor_Upsert]
    @IdProveedor INT = NULL,
    @RazonSocial NVARCHAR(250),
    @Ruc NVARCHAR(20) = NULL,
    @Contacto NVARCHAR(150) = NULL,
    @Telefono NVARCHAR(50) = NULL,
    @Correo NVARCHAR(150) = NULL,
    @Direccion NVARCHAR(250) = NULL,
    @Banco NVARCHAR(50) = NULL,
    @CuentaCorriente NVARCHAR(50) = NULL,
    @CCI NVARCHAR(50) = NULL,
    @CuentaDetraccion NVARCHAR(50) = NULL,
    @DescripcionServicio NVARCHAR(250) = NULL,
    @Observacion NVARCHAR(250) = NULL,
    @TrabajamosConProveedor NVARCHAR(10) = NULL,
    @Activo BIT = 1
AS
BEGIN
    SET
NOCOUNT ON;

SET
XACT_ABORT ON;

IF NULLIF(LTRIM(RTRIM(@RazonSocial)), '') IS NULL
        THROW 51520,
'RazonSocial es requerido.',
1;

SET @RazonSocial = LTRIM(RTRIM(@RazonSocial));
SET @Ruc = NULLIF(LTRIM(RTRIM(@Ruc)), '');

IF @IdProveedor IS NULL
    BEGIN
        IF EXISTS (
SELECT
    1
FROM
    maestra.Proveedor
WHERE
    @Ruc IS NOT NULL AND Ruc = @Ruc)
            THROW 51522,
'Ya existe un proveedor con ese RUC.',
1;

INSERT
    INTO
    maestra.Proveedor
        (
            RazonSocial,
    Ruc,
    Contacto,
    Telefono,
    Correo,
    Direccion,
            Banco,
    CuentaCorriente,
    CCI,
    CuentaDetraccion,
            DescripcionServicio,
    Observacion,
    TrabajamosConProveedor,
    Activo
        )
VALUES
        (
            @RazonSocial,
@Ruc,
@Contacto,
@Telefono,
@Correo,
@Direccion,
            @Banco,
@CuentaCorriente,
@CCI,
@CuentaDetraccion,
            @DescripcionServicio,
@Observacion,
@TrabajamosConProveedor,
@Activo
        );

SELECT
    CAST(SCOPE_IDENTITY() AS INT) AS IdProveedor;
END
ELSE
    BEGIN
        IF EXISTS (
SELECT
1
FROM
maestra.Proveedor
WHERE
@Ruc IS NOT NULL AND Ruc = @Ruc
AND IdProveedor <> @IdProveedor)
            THROW 51523,
'Ya existe otro proveedor con ese RUC.',
1;

UPDATE
    maestra.Proveedor
SET
    RazonSocial = @RazonSocial,
               Ruc = @Ruc,
               Contacto = @Contacto,
               Telefono = @Telefono,
               Correo = @Correo,
               Direccion = @Direccion,
               Banco = @Banco,
               CuentaCorriente = @CuentaCorriente,
               CCI = @CCI,
               CuentaDetraccion = @CuentaDetraccion,
               DescripcionServicio = @DescripcionServicio,
               Observacion = @Observacion,
               TrabajamosConProveedor = @TrabajamosConProveedor,
               Activo = @Activo
WHERE
    IdProveedor = @IdProveedor;

SELECT
    @IdProveedor AS IdProveedor;
END
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_ProveedorEspecialidad_Set]
    @IdProveedor INT,
    @IdEspecialidad INT,
    @Activo BIT = 1
AS
BEGIN
    SET
NOCOUNT ON;

IF NOT EXISTS (
SELECT
    1
FROM
    maestra.Proveedor
WHERE
    IdProveedor = @IdProveedor)
        THROW 50024,
'Proveedor no existe.',
1;

IF NOT EXISTS (
SELECT
    1
FROM
    maestra.Especialidad
WHERE
    IdEspecialidad = @IdEspecialidad)
        THROW 50025,
'Especialidad no existe.',
1;

IF EXISTS (
SELECT
    1
FROM
    maestra.ProveedorEspecialidad
WHERE
    IdProveedor = @IdProveedor
    AND IdEspecialidad = @IdEspecialidad)
    BEGIN
        UPDATE
    maestra.ProveedorEspecialidad
SET
    Activo = @Activo
WHERE
    IdProveedor = @IdProveedor
    AND IdEspecialidad = @IdEspecialidad;
END
ELSE
BEGIN
        INSERT
    INTO
    maestra.ProveedorEspecialidad(IdProveedor,
    IdEspecialidad,
    Activo)
VALUES (@IdProveedor,
@IdEspecialidad,
@Activo);
END

    SELECT
    1 AS Ok;
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_ProveedorEspecialidadCotizacion_List]
    @IdProyecto INT = NULL,
    @IdProveedor INT = NULL,
    @IdEspecialidad INT = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
        pec.IdProveedorEspecialidadCotizacion,
        pec.IdProyecto,
        p.NombreProyecto,
        pec.IdProveedor,
        pr.RazonSocial AS Proveedor,
        pec.IdEspecialidad,
        e.Nombre AS Especialidad,
        pec.Empresa,
        pec.Servicio,
        pec.Moneda,
        pec.MontoCotizacion,
        ISNULL(rv.PorcentajeGarantia, 0.04) AS PorcentajeGarantia,
        ISNULL(rv.PorcentajeDetraccion, 0.04) AS PorcentajeDetraccion
FROM
    maestra.ProveedorEspecialidadCotizacion pec
INNER JOIN maestra.Proveedor pr ON
    pr.IdProveedor = pec.IdProveedor
INNER JOIN maestra.Especialidad e ON
    e.IdEspecialidad = pec.IdEspecialidad
LEFT JOIN maestra.Proyecto p ON
    p.IdProyecto = pec.IdProyecto
LEFT JOIN maestra.ProveedorReglaValorizacion rv ON
    rv.IdProveedor = pec.IdProveedor
    AND rv.Activo = 1
WHERE
    pec.Activo = 1
    AND (@IdProyecto IS NULL
        OR pec.IdProyecto = @IdProyecto)
    AND (@IdProveedor IS NULL
        OR pec.IdProveedor = @IdProveedor)
    AND (@IdEspecialidad IS NULL
        OR pec.IdEspecialidad = @IdEspecialidad)
ORDER BY
    pec.IdProveedorEspecialidadCotizacion DESC;
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_ProveedorEspecialidadCotizacion_Upsert]
    @IdProveedorEspecialidadCotizacion INT = NULL,
    @IdProyecto INT,
    @IdProveedor INT,
    @IdEspecialidad INT,
    @Empresa NVARCHAR(200) = NULL,
    @Servicio NVARCHAR(200),
    @Moneda NVARCHAR(20),
    @MontoCotizacion DECIMAL(18, 2),
    @Activo BIT = 1
AS
BEGIN
    SET
NOCOUNT ON;

IF @IdProveedorEspecialidadCotizacion IS NULL
OR @IdProveedorEspecialidadCotizacion = 0
    BEGIN
        INSERT
    INTO
    maestra.ProveedorEspecialidadCotizacion
        (
            IdProyecto,
    IdProveedor,
    IdEspecialidad,
    Empresa,
    Servicio,
    Moneda,
    MontoCotizacion,
    Activo,
    FechaCreacion
        )
VALUES
        (
            @IdProyecto,
@IdProveedor,
@IdEspecialidad,
@Empresa,
@Servicio,
@Moneda,
@MontoCotizacion,
@Activo,
GETDATE()
        );

SELECT
    SCOPE_IDENTITY() AS IdProveedorEspecialidadCotizacion;
END
ELSE
    BEGIN
        UPDATE
    maestra.ProveedorEspecialidadCotizacion
SET
    IdProyecto = @IdProyecto,
               IdProveedor = @IdProveedor,
               IdEspecialidad = @IdEspecialidad,
               Empresa = @Empresa,
               Servicio = @Servicio,
               Moneda = @Moneda,
               MontoCotizacion = @MontoCotizacion,
               Activo = @Activo
WHERE
    IdProveedorEspecialidadCotizacion = @IdProveedorEspecialidadCotizacion;

SELECT
    @IdProveedorEspecialidadCotizacion AS IdProveedorEspecialidadCotizacion;
END
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_ProveedorReglaValorizacion_Upsert]
    @IdProveedor INT,
    @PorcentajeGarantia DECIMAL(9, 6) = 0.050000,
    @PorcentajeDetraccion DECIMAL(9, 6) = 0.040000
AS
BEGIN
    SET
NOCOUNT ON;

IF EXISTS (
SELECT
    1
FROM
    maestra.ProveedorReglaValorizacion
WHERE
    IdProveedor = @IdProveedor)
    BEGIN
        UPDATE
    maestra.ProveedorReglaValorizacion
SET
    PorcentajeGarantia = @PorcentajeGarantia,
            PorcentajeDetraccion = @PorcentajeDetraccion,
            Activo = 1
WHERE
    IdProveedor = @IdProveedor;
END
ELSE
BEGIN
        INSERT
    INTO
    maestra.ProveedorReglaValorizacion
        (
            IdProveedor,
            PorcentajeGarantia,
            PorcentajeDetraccion
        )
VALUES
        (
            @IdProveedor,
            @PorcentajeGarantia,
            @PorcentajeDetraccion
        );
END
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_Proyecto_List]
    @Activo BIT = NULL
AS
BEGIN
    SET
    NOCOUNT ON;
    
    SELECT
        IdProyecto,
        NombreProyecto,
        Descripcion,
        ISNULL(CotizacionGeneral, 0) AS CotizacionGeneral,
        Activo,
        FechaCreacion
FROM
    maestra.Proyecto
WHERE
    (@Activo IS NULL
        OR Activo = @Activo)
ORDER BY
    NombreProyecto;
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_Proyecto_Upsert]
    @IdProyecto INT = NULL,
    @NombreProyecto NVARCHAR(200),
    @Descripcion NVARCHAR(500) = NULL,
    @CotizacionGeneral DECIMAL(18, 2) = 0,
    @Activo BIT = 1
AS
BEGIN
    SET
NOCOUNT ON;

IF @IdProyecto IS NULL
OR @IdProyecto = 0
    BEGIN
        INSERT
    INTO
    maestra.Proyecto
        (
            NombreProyecto,
            Descripcion,
            CotizacionGeneral,
            Activo,
            FechaCreacion
        )
VALUES
        (
            @NombreProyecto,
            @Descripcion,
            ISNULL(@CotizacionGeneral, 0),
            @Activo,
            GETDATE()
        );

SELECT
    CAST(SCOPE_IDENTITY() AS INT);

RETURN;
END

    UPDATE
    maestra.Proyecto
SET
    NombreProyecto = @NombreProyecto,
           Descripcion = @Descripcion,
           CotizacionGeneral = ISNULL(@CotizacionGeneral, 0),
           Activo = @Activo
WHERE
    IdProyecto = @IdProyecto;

SELECT
    @IdProyecto;
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_UnidadMedida_List]
    @Activo BIT = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
        IdUnidadMedida,
        Codigo,
        Nombre,
        Activo
FROM
    maestra.UnidadMedida
WHERE
    (@Activo IS NULL
        OR Activo = @Activo)
ORDER BY
    Nombre;
END;
GO

CREATE OR ALTER PROCEDURE [maestra].[usp_UnidadMedida_Upsert]
    @IdUnidadMedida INT = NULL,
    @Codigo NVARCHAR(20),
    @Nombre NVARCHAR(100),
    @Activo BIT
AS
BEGIN
    SET
NOCOUNT ON;

SET
@Codigo = UPPER(LTRIM(RTRIM(ISNULL(@Codigo, ''))));

SET
@Nombre = LTRIM(RTRIM(ISNULL(@Nombre, '')));

IF @Codigo = ''
OR @Nombre = ''
        THROW 50001,
'Debes ingresar código y nombre de la unidad de medida.',
1;

IF EXISTS (
SELECT
    1
FROM
    maestra.UnidadMedida
WHERE
    Codigo = @Codigo
    AND (@IdUnidadMedida IS NULL
        OR IdUnidadMedida <> @IdUnidadMedida))
        THROW 50001,
'Ya existe una unidad de medida con ese código.',
1;

IF EXISTS (
SELECT
    1
FROM
    maestra.UnidadMedida
WHERE
    Nombre = @Nombre
    AND (@IdUnidadMedida IS NULL
        OR IdUnidadMedida <> @IdUnidadMedida))
        THROW 50001,
'Ya existe una unidad de medida con ese nombre.',
1;

IF @IdUnidadMedida IS NULL
OR @IdUnidadMedida = 0
    BEGIN
        INSERT
    INTO
    maestra.UnidadMedida (Codigo,
    Nombre,
    Activo,
    FechaCreacion)
VALUES (@Codigo,
@Nombre,
@Activo,
GETDATE());

SELECT
    CAST(SCOPE_IDENTITY() AS INT);

RETURN;
END

    UPDATE
    maestra.UnidadMedida
SET
    Codigo = @Codigo,
           Nombre = @Nombre,
           Activo = @Activo
WHERE
    IdUnidadMedida = @IdUnidadMedida;

SELECT
    @IdUnidadMedida;
END;
