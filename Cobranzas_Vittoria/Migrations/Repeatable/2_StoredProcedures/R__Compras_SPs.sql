CREATE OR ALTER PROCEDURE [compras].[usp_Compra_Aceptar]
    @IdCompra INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    
    DECLARE
        @IdOrdenCompra INT,
        @FechaCompra DATE,
        @Aceptada BIT;

    SELECT
        @IdOrdenCompra = IdOrdenCompra,
        @FechaCompra = FechaCompra,
        @Aceptada = Aceptada

    FROM compras.Compra
    WHERE IdCompra = @IdCompra;

    IF @IdOrdenCompra IS NULL
        THROW 50065, 'Compra no existe.',
    1;

    IF ISNULL(@Aceptada, 0) = 1
    BEGIN
        SELECT
            1 AS Ok,
            N'La compra ya estaba aceptada.' AS Mensaje;

        RETURN;
    END

    BEGIN TRAN;

        UPDATE
        compras.Compra
        SET
            Aceptada = 1
        WHERE
            IdCompra = @IdCompra;

    INSERT INTO
        almacen.KardexMovimiento
        (
            IdMaterial,
            IdEspecialidad,
            TipoMovimiento,
            FechaMovimiento,
            CantidadEntrada,
            CantidadSalida,
            StockResultante,
            IdCompra,
            IdOrdenCompra,
            Observacion,
            FechaIngresoAlmacen,
            FechaSalidaAlmacen,
            FechaCreacion
        )
    SELECT
        cd.IdMaterial,
        m.IdEspecialidad,
        N'ENTRADA',
        @FechaCompra,
        cd.Cantidad,
        0,
        ISNULL((
            SELECT TOP 1 km.StockResultante
            FROM almacen.KardexMovimiento km
            WHERE km.IdMaterial = cd.IdMaterial
            ORDER BY km.FechaMovimiento DESC, km.IdKardexMovimiento DESC
            ), 0) + cd.Cantidad,
            @IdCompra,
            @IdOrdenCompra,
            N'Ingreso por compra aceptada',
            @FechaCompra,
            NULL,
            GETDATE()

    FROM compras.CompraDetalle cd
    INNER JOIN maestra.Material m ON m.IdMaterial = cd.IdMaterial

    WHERE cd.IdCompra = @IdCompra
    AND NOT EXISTS (
        SELECT
            1
        FROM almacen.KardexMovimiento km
        WHERE km.IdCompra = @IdCompra
        AND km.IdMaterial = cd.IdMaterial
    );

    COMMIT;

    SELECT
        1 AS Ok,
        N'Compra aceptada y kardex actualizado.' AS Mensaje;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_Compra_Get]
    @IdCompra INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.IdCompra,
        c.NumeroCompra,
        c.IdOrdenCompra,
        oc.NumeroOrdenCompra,
        c.IdProveedor,
        p.RazonSocial AS Proveedor,
        c.FechaCompra,
        c.Aceptada,
        c.IncluyeIGV,
        c.SubtotalSinIGV,
        c.MontoIGV,
        c.MontoTotal,
        c.Observacion,
        c.FechaCreacion
    
    FROM compras.Compra c
    INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
    INNER JOIN maestra.Proveedor p ON p.IdProveedor = c.IdProveedor
    
    WHERE c.IdCompra = @IdCompra;

    SELECT
        cd.IdCompraDetalle,
        cd.IdCompra,
        cd.IdMaterial,
        m.Descripcion AS Material,
        m.UnidadMedida,
        cd.Cantidad,
        cd.PrecioUnitario,
        cd.Subtotal
    
    FROM compras.CompraDetalle cd
    INNER JOIN maestra.Material m ON m.IdMaterial = cd.IdMaterial
    WHERE cd.IdCompra = @IdCompra
    ORDER BY cd.IdCompraDetalle;

    SELECT
        IdCompraDocumento,
        IdCompra,
        TipoDocumento,
        NumeroDocumento,
        RutaArchivo,
        FechaDocumento,
        Monto,
        Observacion,
        NombreArchivo,
        Extension,
        FechaCreacion
    FROM compras.CompraDocumento
    WHERE IdCompra = @IdCompra
    ORDER BY IdCompraDocumento;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_Compra_List]
    @Aceptada BIT = NULL,
    @IdProveedor INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.IdCompra,
        c.NumeroCompra,
        c.IdOrdenCompra,
        oc.NumeroOrdenCompra,
        c.IdProveedor,
        p.RazonSocial AS Proveedor,
        c.FechaCompra,
        c.Aceptada,
        c.IncluyeIGV,
        c.SubtotalSinIGV,
        c.MontoIGV,
        c.MontoTotal,
        c.Observacion,
        c.FechaCreacion
    
    FROM compras.Compra c
    INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
    INNER JOIN maestra.Proveedor p ON p.IdProveedor = c.IdProveedor
    
    WHERE (@Aceptada IS NULL OR c.Aceptada = @Aceptada)
    AND (@IdProveedor IS NULL OR c.IdProveedor = @IdProveedor)
    ORDER BY c.IdCompra DESC;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_Compra_Registrar]
    @NumeroCompra NVARCHAR(30),
    @IdOrdenCompra INT,
    @IdProveedor INT,
    @FechaCompra DATE,
    @IncluyeIGV BIT = 0,
    @Observacion NVARCHAR(250) = NULL,
    @Items compras.TVP_CompraDetalle READONLY,
    @Documentos compras.TVP_CompraDocumento READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@NumeroCompra)), '') IS NULL
        THROW 50060, 'NumeroCompra es requerido.',
    1;

    IF EXISTS (
        SELECT
            1
        FROM compras.Compra
        WHERE NumeroCompra = @NumeroCompra)
            THROW 50061, 'Ya existe el Número de Compra.',
    1;

    IF NOT EXISTS (
        SELECT
            1
        FROM compras.OrdenCompra
        WHERE IdOrdenCompra = @IdOrdenCompra)
            THROW 50062, 'Orden de compra no existe.',
    1;

    IF NOT EXISTS (
        SELECT
            1
        FROM compras.OrdenCompra
        WHERE IdOrdenCompra = @IdOrdenCompra)
            THROW 50062, 'Orden de compra no existe.',
    1;

    IF NOT EXISTS (
        SELECT
            1
        FROM maestra.Proveedor
        WHERE IdProveedor = @IdProveedor)
            THROW 50063, 'Proveedor no existe.',
    1;

    IF NOT EXISTS (
        SELECT
            1
        FROM @Items)
            THROW 50064, 'Debe registrar items en la compra.',
    1;

    DECLARE
        @MontoBruto DECIMAL(18, 2),
        @SubtotalSinIGV DECIMAL(18, 2),
        @MontoIGV DECIMAL(18, 2),
        @MontoTotal DECIMAL(18, 2);

    SELECT
        @MontoBruto = ROUND(SUM(Cantidad * PrecioUnitario), 2)
    FROM @Items;

    SET @MontoTotal = ISNULL(@MontoBruto, 0);
    SET @SubtotalSinIGV = CASE
    WHEN ISNULL(@IncluyeIGV, 0) = 1 THEN ROUND(@MontoTotal / 1.18, 2)
    ELSE @MontoTotal
END;

SET @MontoIGV = CASE
    WHEN ISNULL(@IncluyeIGV, 0) = 1 THEN ROUND(@MontoTotal - @SubtotalSinIGV, 2)
ELSE 0
END;

BEGIN TRAN;

INSERT INTO
    compras.Compra
    (
        NumeroCompra,
        IdOrdenCompra,
        IdProveedor,
        FechaCompra,
        Aceptada,
        IncluyeIGV,
        SubtotalSinIGV,
        MontoIGV,
        MontoTotal,
        Observacion,
        FechaCreacion
    ) VALUES (
        @NumeroCompra,
        @IdOrdenCompra,
        @IdProveedor,
        @FechaCompra,
        0,
        @IncluyeIGV,
        @SubtotalSinIGV,
        @MontoIGV,
        @MontoTotal,
        @Observacion,
        GETDATE()
    );

DECLARE @IdCompra INT = SCOPE_IDENTITY();

INSERT INTO
    compras.CompraDetalle
    (
        IdCompra,
        IdMaterial,
        Cantidad,
        PrecioUnitario
    ) SELECT
        @IdCompra,
        IdMaterial,
        Cantidad,
        PrecioUnitario

    FROM @Items;

INSERT INTO
    compras.CompraDocumento
    (
        IdCompra,
        TipoDocumento,
        NumeroDocumento,
        RutaArchivo,
        FechaDocumento,
        Monto,
        Observacion,
        NombreArchivo,
        Extension,
        FechaCreacion
    ) SELECT
        @IdCompra,
        TipoDocumento,
        NumeroDocumento,
        RutaArchivo,
        FechaDocumento,
        Monto,
        Observacion,
        RIGHT(RutaArchivo, CHARINDEX('/', REVERSE(RutaArchivo + '/')) - 1),
        CASE
        WHEN CHARINDEX('.', RutaArchivo) > 0 THEN RIGHT(RutaArchivo, CHARINDEX('.', REVERSE(RutaArchivo)) - 1)
        ELSE NULL
    END,
        GETDATE()
FROM
    @Documentos;

UPDATE
    compras.OrdenCompra
SET
    Estado = N'Atendida'
WHERE
    IdOrdenCompra = @IdOrdenCompra;

COMMIT;

SELECT
    @IdCompra AS IdCompra,
            @SubtotalSinIGV AS SubtotalSinIGV,
            @MontoIGV AS MontoIGV,
            @MontoTotal AS MontoTotal,
            @IncluyeIGV AS IncluyeIGV;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_OrdenCompra_Actualizar]
(
    @IdOrdenCompra INT,
    @NumeroOrdenCompra NVARCHAR(50),
    @IdRequerimiento INT,
    @IdProveedor INT,
    @IdProyecto INT,
    @FechaOrdenCompra DATE,
    @Descripcion NVARCHAR(500) = NULL,
    @IdUsuarioCreacion INT = NULL,
    @RutaPdf NVARCHAR(500) = NULL,
    @Items compras.TVP_OrdenCompraDetalle READONLY
)
AS
BEGIN
    SET
NOCOUNT ON;

SET
XACT_ABORT ON;

IF NOT EXISTS (
SELECT
    1
FROM
    compras.OrdenCompra
WHERE
    IdOrdenCompra = @IdOrdenCompra)
    BEGIN
        RAISERROR('La orden de compra no existe.', 16, 1);

RETURN;
END;

IF NOT EXISTS (
SELECT
    1
FROM
    @Items)
    BEGIN
        RAISERROR('La orden debe tener items.', 16, 1);

RETURN;
END;

IF EXISTS (
SELECT
    1
FROM
    @Items
WHERE
    IdProveedor IS NULL
    OR IdProveedor <= 0)
    BEGIN
        RAISERROR('Cada material debe tener proveedor.', 16, 1);

RETURN;
END;

DECLARE @IdProveedorCabecera INT =
        COALESCE(NULLIF(@IdProveedor, 0), (SELECT TOP 1 IdProveedor FROM @Items ORDER BY IdProveedor));

BEGIN TRANSACTION;

BEGIN TRY
        UPDATE
    compras.OrdenCompra
SET
    NumeroOrdenCompra = @NumeroOrdenCompra,
               IdRequerimiento = @IdRequerimiento,
               IdProveedor = @IdProveedorCabecera,
               IdProyecto = @IdProyecto,
               FechaOrdenCompra = @FechaOrdenCompra,
               Descripcion = @Descripcion,
               RutaPdf = @RutaPdf
WHERE
    IdOrdenCompra = @IdOrdenCompra;

DELETE
FROM
    compras.OrdenCompraDetalle
WHERE
    IdOrdenCompra = @IdOrdenCompra;

INSERT
    INTO
    compras.OrdenCompraDetalle
            (IdOrdenCompra,
    IdMaterial,
    Cantidad,
    IdProveedor,
    PrecioUnitario)
        SELECT
            @IdOrdenCompra,
    i.IdMaterial,
    i.Cantidad,
    i.IdProveedor,
    i.PrecioUnitario
FROM
    @Items i;

UPDATE
    oc
SET
    oc.Total = ISNULL(t.Total, 0),
               oc.IdProveedor = CASE
        WHEN t.CantProv = 1 THEN t.IdProveedorUnico
        ELSE oc.IdProveedor
    END
FROM
    compras.OrdenCompra oc
        OUTER APPLY
        (
    SELECT
                SUM(d.Cantidad * d.PrecioUnitario) AS Total,
                COUNT(DISTINCT d.IdProveedor) AS CantProv,
                MIN(d.IdProveedor) AS IdProveedorUnico
    FROM
        compras.OrdenCompraDetalle d
    WHERE
        d.IdOrdenCompra = oc.IdOrdenCompra
        ) t
WHERE
    oc.IdOrdenCompra = @IdOrdenCompra;

COMMIT TRANSACTION;
END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

THROW;
END CATCH
END;


CREATE OR ALTER PROCEDURE [compras].[usp_OrdenCompra_ActualizarEstado]
    @IdOrdenCompra INT,
    @EstadoNuevo NVARCHAR(30),
    @IdUsuario INT = NULL,
    @Observacion NVARCHAR(250) = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SET
XACT_ABORT ON;

IF @EstadoNuevo NOT IN (N'Generada', N'Aprobada', N'Enviada', N'Atendida', N'Anulada')
        THROW 50056,
'Estado inválido para orden de compra.',
1;

DECLARE @EstadoAnterior NVARCHAR(30);

SELECT
    @EstadoAnterior = Estado
FROM
    compras.OrdenCompra
WHERE
    IdOrdenCompra = @IdOrdenCompra;

IF @EstadoAnterior IS NULL
        THROW 50057,
'Orden de compra no existe.',
1;

UPDATE
    compras.OrdenCompra
SET
    Estado = @EstadoNuevo
WHERE
    IdOrdenCompra = @IdOrdenCompra;

INSERT
    INTO
    compras.OrdenCompraHistorial
    (
        IdOrdenCompra,
    EstadoAnterior,
    EstadoNuevo,
    IdUsuario,
    Observacion
    )
VALUES
    (
        @IdOrdenCompra,
@EstadoAnterior,
@EstadoNuevo,
@IdUsuario,
@Observacion
    );

SELECT
    1 AS Ok;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_OrdenCompra_CrearDesdeRequerimiento]
(
    @NumeroOrdenCompra NVARCHAR(50),
    @IdRequerimiento INT,
    @IdProveedor INT,
    @IdProyecto INT,
    @FechaOrdenCompra DATE,
    @Descripcion NVARCHAR(500) = NULL,
    @IdUsuarioCreacion INT = NULL,
    @RutaPdf NVARCHAR(500) = NULL,
    @Items compras.TVP_OrdenCompraDetalle READONLY
)
AS
BEGIN
    SET
NOCOUNT ON;

SET
XACT_ABORT ON;

IF NOT EXISTS (
SELECT
    1
FROM
    compras.Requerimiento r
WHERE
    r.IdRequerimiento = @IdRequerimiento
    AND UPPER(ISNULL(r.Estado, '')) = 'ENVIADOOC'
    )
    BEGIN
        RAISERROR('La orden de compra solo puede generarse desde un requerimiento enviado a OC.', 16, 1);

RETURN;
END;

IF EXISTS (
SELECT
    1
FROM
    compras.OrdenCompra oc
WHERE
    oc.IdRequerimiento = @IdRequerimiento
    )
    BEGIN
        RAISERROR('El requerimiento ya tiene una orden de compra generada.', 16, 1);

RETURN;
END;

IF NOT EXISTS (
SELECT
    1
FROM
    @Items)
    BEGIN
        RAISERROR('La orden debe tener items.', 16, 1);

RETURN;
END;

IF EXISTS (
SELECT
    1
FROM
    @Items
WHERE
    IdProveedor IS NULL
    OR IdProveedor <= 0)
    BEGIN
        RAISERROR('Cada material debe tener proveedor.', 16, 1);

RETURN;
END;

DECLARE @IdProveedorCabecera INT =
        COALESCE(NULLIF(@IdProveedor, 0), (SELECT TOP 1 IdProveedor FROM @Items ORDER BY IdProveedor));

BEGIN TRANSACTION;

BEGIN TRY
        INSERT
    INTO
    compras.OrdenCompra
        (
            NumeroOrdenCompra,
    IdRequerimiento,
    IdProveedor,
    IdProyecto,
    FechaOrdenCompra,
            Descripcion,
    Estado,
    Total,
    RutaPdf,
    FechaCreacion,
    IdUsuarioCreacion
        )
VALUES
        (
            @NumeroOrdenCompra,
@IdRequerimiento,
@IdProveedorCabecera,
@IdProyecto,
@FechaOrdenCompra,
            @Descripcion,
'Registrada',
0,
@RutaPdf,
GETDATE(),
@IdUsuarioCreacion
        );

DECLARE @IdOrdenCompra INT = SCOPE_IDENTITY();

INSERT
    INTO
    compras.OrdenCompraDetalle
            (IdOrdenCompra,
    IdMaterial,
    Cantidad,
    IdProveedor,
    PrecioUnitario)
        SELECT
            @IdOrdenCompra,
    i.IdMaterial,
    i.Cantidad,
    i.IdProveedor,
    i.PrecioUnitario
FROM
    @Items i;

UPDATE
    oc
SET
    oc.Total = ISNULL(t.Total, 0),
               oc.IdProveedor = CASE
        WHEN t.CantProv = 1 THEN t.IdProveedorUnico
        ELSE oc.IdProveedor
    END
FROM
    compras.OrdenCompra oc
        OUTER APPLY
        (
    SELECT
                SUM(d.Cantidad * d.PrecioUnitario) AS Total,
                COUNT(DISTINCT d.IdProveedor) AS CantProv,
                MIN(d.IdProveedor) AS IdProveedorUnico
    FROM
        compras.OrdenCompraDetalle d
    WHERE
        d.IdOrdenCompra = oc.IdOrdenCompra
        ) t
WHERE
    oc.IdOrdenCompra = @IdOrdenCompra;

UPDATE
    compras.Requerimiento
SET
    Estado = 'GeneradoOC'
WHERE
    IdRequerimiento = @IdRequerimiento;

SELECT
            @IdOrdenCompra AS IdOrdenCompra,
            CAST(ISNULL(
                (SELECT SUM(d.Cantidad * d.PrecioUnitario)
                 FROM compras.OrdenCompraDetalle d
                 WHERE d.IdOrdenCompra = @IdOrdenCompra), 0
            ) AS DECIMAL(18, 2)) AS Total;

COMMIT TRANSACTION;
END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

THROW;
END CATCH
END;

CREATE OR ALTER PROCEDURE [compras].[usp_OrdenCompra_Get]
(
    @IdOrdenCompra INT
)
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
        oc.IdOrdenCompra,
        oc.NumeroOrdenCompra,
        oc.IdRequerimiento,
        oc.IdProveedor,
        p.RazonSocial AS Proveedor,
        oc.IdProyecto,
        pr.NombreProyecto,
        oc.FechaOrdenCompra,
        oc.Descripcion,
        oc.Estado,
        oc.Total,
        oc.RutaPdf,
        oc.FechaCreacion,
        oc.IdUsuarioCreacion
FROM
    compras.OrdenCompra oc
LEFT JOIN maestra.Proveedor p ON
    p.IdProveedor = oc.IdProveedor
LEFT JOIN maestra.Proyecto pr ON
    pr.IdProyecto = oc.IdProyecto
WHERE
    oc.IdOrdenCompra = @IdOrdenCompra;

SELECT
        d.IdOrdenCompraDetalle,
        d.IdOrdenCompra,
        d.IdMaterial,
        m.Descripcion AS Material,
        COALESCE(m.UnidadMedida, '-') AS UnidadMedida,
        d.Cantidad,
        ISNULL(d.IdProveedor, oc.IdProveedor) AS IdProveedor,
        p.RazonSocial AS Proveedor,
        d.PrecioUnitario,
        CAST(d.Cantidad * d.PrecioUnitario AS DECIMAL(18, 2)) AS Subtotal
FROM
    compras.OrdenCompraDetalle d
INNER JOIN compras.OrdenCompra oc ON
    oc.IdOrdenCompra = d.IdOrdenCompra
INNER JOIN maestra.Material m ON
    m.IdMaterial = d.IdMaterial
LEFT JOIN maestra.Proveedor p ON
    p.IdProveedor = ISNULL(d.IdProveedor, oc.IdProveedor)
WHERE
    d.IdOrdenCompra = @IdOrdenCompra
ORDER BY
    d.IdOrdenCompraDetalle;

IF OBJECT_ID('compras.HistorialOrdenCompra', 'U') IS NOT NULL
    BEGIN
        SELECT
            h.IdHistorialOrdenCompra,
            h.IdOrdenCompra,
            h.Fecha,
            h.Accion,
            h.Observacion,
            h.IdUsuario,
            u.Nombres + ' ' + ISNULL(u.Apellidos, '') AS Usuario
FROM
    compras.HistorialOrdenCompra h
LEFT JOIN seguridad.Usuario u ON
    u.IdUsuario = h.IdUsuario
WHERE
    h.IdOrdenCompra = @IdOrdenCompra
ORDER BY
    h.Fecha DESC;
END
ELSE
    BEGIN
        SELECT
            CAST(NULL AS INT) AS IdHistorialOrdenCompra,
            CAST(NULL AS INT) AS IdOrdenCompra,
            CAST(NULL AS DATETIME) AS Fecha,
            CAST(NULL AS NVARCHAR(100)) AS Accion,
            CAST(NULL AS NVARCHAR(500)) AS Observacion,
            CAST(NULL AS INT) AS IdUsuario,
            CAST(NULL AS NVARCHAR(200)) AS Usuario
WHERE
    1 = 0;
END
END;

CREATE OR ALTER PROCEDURE [compras].[usp_OrdenCompra_List]
(
    @Estado NVARCHAR(20) = NULL,
    @IdProveedor INT = NULL,
    @IdProyecto INT = NULL
)
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
        oc.IdOrdenCompra,
        oc.NumeroOrdenCompra,
        oc.IdRequerimiento,
        r.NumeroRequerimiento,
        oc.IdProveedor,
        CASE
            WHEN EXISTS (
        SELECT
            1
        FROM
            compras.OrdenCompraDetalle d
        WHERE
            d.IdOrdenCompra = oc.IdOrdenCompra
        GROUP BY
            d.IdOrdenCompra
        HAVING
            COUNT(DISTINCT d.IdProveedor) > 1
            ) THEN 'Múltiples'
        ELSE p.RazonSocial
    END AS Proveedor,
        oc.IdProyecto,
        pr.NombreProyecto,
        oc.FechaOrdenCompra,
        oc.Estado,
        oc.Total
FROM
    compras.OrdenCompra oc
LEFT JOIN compras.Requerimiento r ON
    r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proveedor p ON
    p.IdProveedor = oc.IdProveedor
LEFT JOIN maestra.Proyecto pr ON
    pr.IdProyecto = oc.IdProyecto
WHERE
    (@Estado IS NULL
        OR oc.Estado = @Estado)
    AND (@IdProyecto IS NULL
        OR oc.IdProyecto = @IdProyecto)
    AND (
            @IdProveedor IS NULL
        OR oc.IdProveedor = @IdProveedor
        OR EXISTS (
        SELECT
            1
        FROM
            compras.OrdenCompraDetalle d
        WHERE
            d.IdOrdenCompra = oc.IdOrdenCompra
            AND d.IdProveedor = @IdProveedor
            )
          )
ORDER BY
    oc.IdOrdenCompra DESC;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_Requerimiento_Actualizar]
(
    @IdRequerimiento INT,
    @NumeroRequerimiento NVARCHAR(50),
    @FechaRequerimiento DATE,
    @IdEspecialidad INT,
    @IdProyecto INT,
    @Descripcion NVARCHAR(500) = NULL,
    @FechaEntrega DATE = NULL,
    @IdUsuarioSolicitante INT,
    @Observacion NVARCHAR(500) = NULL,
    @Items compras.TVP_RequerimientoDetalle READONLY
)
AS
BEGIN
    SET
NOCOUNT ON;

IF EXISTS (
SELECT
    1
FROM
    compras.Requerimiento r
WHERE
    r.IdRequerimiento = @IdRequerimiento
    AND UPPER(ISNULL(r.Estado, '')) <> 'REGISTRADO'
    )
    BEGIN
        RAISERROR('El requerimiento solo puede editarse cuando está en estado Registrado.', 16, 1);

RETURN;
END;

IF EXISTS (
SELECT
    1
FROM
    compras.OrdenCompra oc
WHERE
    oc.IdRequerimiento = @IdRequerimiento
    )
    BEGIN
        RAISERROR('El requerimiento ya tiene una orden de compra asociada y no puede editarse.', 16, 1);

RETURN;
END;

UPDATE
    compras.Requerimiento
SET
    NumeroRequerimiento = @NumeroRequerimiento,
           FechaRequerimiento = @FechaRequerimiento,
           IdEspecialidad = @IdEspecialidad,
           IdProyecto = @IdProyecto,
           Descripcion = @Descripcion,
           FechaEntrega = @FechaEntrega,
           IdUsuarioSolicitante = @IdUsuarioSolicitante,
           Observacion = @Observacion
WHERE
    IdRequerimiento = @IdRequerimiento;

DELETE
FROM
    compras.RequerimientoDetalle
WHERE
    IdRequerimiento = @IdRequerimiento;

INSERT
    INTO
    compras.RequerimientoDetalle (IdRequerimiento,
    IdMaterial,
    Cantidad,
    Observacion)
    SELECT
    @IdRequerimiento,
    i.IdMaterial,
    i.Cantidad,
    i.Observacion
FROM
    @Items i;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_Requerimiento_ActualizarEstado]
(
    @IdRequerimiento INT,
    @Estado NVARCHAR(50),
    @Observacion NVARCHAR(500) = NULL
)
AS
BEGIN
    SET
NOCOUNT ON;

IF UPPER(ISNULL(@Estado, '')) = 'ENVIADOOC'
    BEGIN
        IF EXISTS (
SELECT
    1
FROM
    compras.Requerimiento
WHERE
    IdRequerimiento = @IdRequerimiento
    AND UPPER(ISNULL(Estado, '')) <> 'REGISTRADO'
        )
        BEGIN
            RAISERROR('Solo se puede enviar a orden de compra un requerimiento en estado Registrado.', 16, 1);

RETURN;
END;

IF EXISTS (
SELECT
    1
FROM
    compras.OrdenCompra
WHERE
    IdRequerimiento = @IdRequerimiento
        )
        BEGIN
            RAISERROR('El requerimiento ya tiene una orden de compra asociada.', 16, 1);

RETURN;
END;
END;

UPDATE
    compras.Requerimiento
SET
    Estado = CASE
            WHEN UPPER(@Estado) = 'REGISTRADO' THEN 'Registrado'
        WHEN UPPER(@Estado) = 'VALIDADOALMACEN' THEN 'ValidadoAlmacen'
        WHEN UPPER(@Estado) = 'ENVIADOOC' THEN 'EnviadoOC'
        WHEN UPPER(@Estado) = 'GENERADOOC' THEN 'GeneradoOC'
        WHEN UPPER(@Estado) = 'ANULADO' THEN 'Anulado'
        ELSE @Estado
    END,
       Observacion = COALESCE(@Observacion, Observacion)
WHERE
    IdRequerimiento = @IdRequerimiento;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_Requerimiento_Crear]
    @NumeroRequerimiento NVARCHAR(30),
    @FechaRequerimiento DATE,
    @IdEspecialidad INT,
    @IdProyecto INT,
    @Descripcion NVARCHAR(250) = NULL,
    @FechaEntrega DATE = NULL,
    @IdUsuarioSolicitante INT,
    @Observacion NVARCHAR(250) = NULL,
    @Items compras.TVP_RequerimientoDetalle READONLY
AS
BEGIN
    SET
    NOCOUNT ON;
    
    SET
    XACT_ABORT ON;
    
    IF NULLIF(LTRIM(RTRIM(@NumeroRequerimiento)), '') IS NULL
        THROW 50040,
    'NumeroRequerimiento es requerido.',
    1;

IF EXISTS (
SELECT
    1
FROM
    compras.Requerimiento
WHERE
    NumeroRequerimiento = @NumeroRequerimiento)
        THROW 50041,
'Ya existe el Número de Requerimiento.',
1;

IF NOT EXISTS (
SELECT
    1
FROM
    maestra.Especialidad
WHERE
    IdEspecialidad = @IdEspecialidad)
        THROW 50042,
'Especialidad no existe.',
1;

IF NOT EXISTS (
SELECT
    1
FROM
    maestra.Proyecto
WHERE
    IdProyecto = @IdProyecto)
        THROW 50043,
'Proyecto no existe.',
1;

IF NOT EXISTS (
SELECT
    1
FROM
    seguridad.Usuario
WHERE
    IdUsuario = @IdUsuarioSolicitante)
        THROW 50044,
'Usuario solicitante no existe.',
1;

IF NOT EXISTS (
SELECT
    1
FROM
    @Items)
        THROW 50045,
'Debe registrar al menos un item.',
1;

BEGIN TRAN;

INSERT
    INTO
    compras.Requerimiento
    (
        NumeroRequerimiento,
    FechaRequerimiento,
    IdEspecialidad,
    IdProyecto,
        Descripcion,
    FechaEntrega,
    IdUsuarioSolicitante,
    Estado,
    Observacion
    )
VALUES
    (
        @NumeroRequerimiento,
@FechaRequerimiento,
@IdEspecialidad,
@IdProyecto,
        @Descripcion,
@FechaEntrega,
@IdUsuarioSolicitante,
N'Registrado',
@Observacion
    );

DECLARE @IdRequerimiento INT = SCOPE_IDENTITY();

INSERT
    INTO
    compras.RequerimientoDetalle
    (
        IdRequerimiento,
    IdMaterial,
    Cantidad,
    Observacion
    )
    SELECT
    @IdRequerimiento,
    IdMaterial,
    Cantidad,
    Observacion
FROM
    @Items;

COMMIT;

SELECT
    @IdRequerimiento AS IdRequerimiento;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_Requerimiento_Get]
    @IdRequerimiento INT
AS
BEGIN
    SET
NOCOUNT ON;
    SELECT
    r.IdRequerimiento,
            r.NumeroRequerimiento,
            r.FechaRequerimiento,
            r.IdEspecialidad,
            e.Nombre AS Especialidad,
            r.IdProyecto,
            p.NombreProyecto,
            r.Descripcion,
            r.FechaEntrega,
            r.IdUsuarioSolicitante,
            u.Nombres + ISNULL(N' ' + u.Apellidos, N'') AS Solicitante,
            r.Estado,
            r.Observacion,
            r.FechaCreacion
FROM
    compras.Requerimiento r
INNER JOIN maestra.Especialidad e ON
    e.IdEspecialidad = r.IdEspecialidad
INNER JOIN maestra.Proyecto p ON
    p.IdProyecto = r.IdProyecto
INNER JOIN seguridad.Usuario u ON
    u.IdUsuario = r.IdUsuarioSolicitante
WHERE
    r.IdRequerimiento = @IdRequerimiento;
    SELECT
    rd.IdRequerimientoDetalle,
            rd.IdRequerimiento,
            rd.IdMaterial,
            m.Descripcion AS Material,
            m.UnidadMedida,
            rd.Cantidad,
            rd.Observacion
FROM
    compras.RequerimientoDetalle rd
INNER JOIN maestra.Material m ON
    m.IdMaterial = rd.IdMaterial
WHERE
    rd.IdRequerimiento = @IdRequerimiento
ORDER BY
    rd.IdRequerimientoDetalle;
    SELECT
    rv.IdRequerimientoValidacion,
            rv.IdRequerimiento,
            rv.IdUsuario,
            u.Nombres + ISNULL(N' ' + u.Apellidos, N'') AS Usuario,
            rv.FechaValidacion,
            rv.Resultado,
            rv.Observacion
FROM
    compras.RequerimientoValidacion rv
INNER JOIN seguridad.Usuario u ON
    u.IdUsuario = rv.IdUsuario
WHERE
    rv.IdRequerimiento = @IdRequerimiento
ORDER BY
    rv.IdRequerimientoValidacion DESC;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_Requerimiento_List]
    @Estado NVARCHAR(30) = NULL,
    @IdEspecialidad INT = NULL,
    @IdProyecto INT = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
    r.IdRequerimiento,
            r.NumeroRequerimiento,
            r.FechaRequerimiento,
            e.Nombre AS Especialidad,
            p.NombreProyecto,
            r.Descripcion,
            r.FechaEntrega,
            u.Nombres + ISNULL(N' ' + u.Apellidos, N'') AS Solicitante,
            r.Estado,
            r.Observacion,
            r.FechaCreacion
FROM
    compras.Requerimiento r
INNER JOIN maestra.Especialidad e ON
    e.IdEspecialidad = r.IdEspecialidad
INNER JOIN maestra.Proyecto p ON
    p.IdProyecto = r.IdProyecto
INNER JOIN seguridad.Usuario u ON
    u.IdUsuario = r.IdUsuarioSolicitante
WHERE
    (@Estado IS NULL
        OR r.Estado = @Estado)
    AND (@IdEspecialidad IS NULL
        OR r.IdEspecialidad = @IdEspecialidad)
    AND (@IdProyecto IS NULL
        OR r.IdProyecto = @IdProyecto)
ORDER BY
    r.IdRequerimiento DESC;
END;

CREATE OR ALTER PROCEDURE [compras].[usp_Requerimiento_ValidarAlmacen]
    @IdRequerimiento INT,
    @IdUsuario INT,
    @Resultado NVARCHAR(20),
    @Observacion NVARCHAR(250) = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SET
XACT_ABORT ON;

IF @Resultado NOT IN (N'Conforme', N'Observado')
        THROW 50047,
'Resultado inválido.',
1;

IF NOT EXISTS (
SELECT
    1
FROM
    compras.Requerimiento
WHERE
    IdRequerimiento = @IdRequerimiento)
        THROW 50048,
'Requerimiento no existe.',
1;

IF NOT EXISTS (
SELECT
    1
FROM
    seguridad.Usuario
WHERE
    IdUsuario = @IdUsuario)
        THROW 50049,
'Usuario no existe.',
1;

BEGIN TRAN;

INSERT
    INTO
    compras.RequerimientoValidacion
    (
        IdRequerimiento,
    IdUsuario,
    Resultado,
    Observacion
    )
VALUES
    (
        @IdRequerimiento,
@IdUsuario,
@Resultado,
@Observacion
    );

UPDATE
    compras.Requerimiento
SET
    Estado = CASE
        WHEN @Resultado = N'Conforme' THEN N'ValidadoAlmacen'
        ELSE Estado
    END,
           Observacion = COALESCE(@Observacion, Observacion)
WHERE
    IdRequerimiento = @IdRequerimiento;

COMMIT;

SELECT
    1 AS Ok;
END;
