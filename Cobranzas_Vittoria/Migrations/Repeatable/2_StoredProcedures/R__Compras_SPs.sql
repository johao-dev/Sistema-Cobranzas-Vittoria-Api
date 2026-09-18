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
GO

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
        proveedores.Proveedores,
        oc.IdMoneda, mon.Codigo AS CodigoMoneda, mon.Simbolo AS SimboloMoneda,
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
    INNER JOIN maestra.Moneda mon ON mon.IdMoneda = oc.IdMoneda
OUTER APPLY (
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.RazonSocial), ', ') AS Proveedores
    FROM (SELECT DISTINCT p.IdProveedor, p.RazonSocial
          FROM compras.CompraDetalle cd
          JOIN compras.OrdenCompraDetalle od ON od.IdOrdenCompra = c.IdOrdenCompra AND od.IdMaterial = cd.IdMaterial
          JOIN maestra.Proveedor p ON p.IdProveedor = od.IdProveedor
          WHERE cd.IdCompra = c.IdCompra) q
) proveedores

    
    WHERE c.IdCompra = @IdCompra;

    SELECT
        cd.IdCompraDetalle,
        cd.IdCompra,
        cd.IdMaterial,
        m.Descripcion AS Material,
        m.UnidadMedida,
        cd.Cantidad,
        cd.PrecioUnitario,
        cd.Subtotal, od.IdProveedor, p.RazonSocial AS Proveedor
    
    FROM compras.CompraDetalle cd
    INNER JOIN maestra.Material m ON m.IdMaterial = cd.IdMaterial
    JOIN compras.Compra c ON c.IdCompra = cd.IdCompra
    LEFT JOIN compras.OrdenCompraDetalle od ON od.IdOrdenCompra = c.IdOrdenCompra AND od.IdMaterial = cd.IdMaterial
    LEFT JOIN maestra.Proveedor p ON p.IdProveedor = od.IdProveedor
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
GO

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
        proveedores.Proveedores,
        oc.IdMoneda, mon.Codigo AS CodigoMoneda, mon.Simbolo AS SimboloMoneda,
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
    INNER JOIN maestra.Moneda mon ON mon.IdMoneda = oc.IdMoneda
OUTER APPLY (
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.RazonSocial), ', ') AS Proveedores
    FROM (SELECT DISTINCT p.IdProveedor, p.RazonSocial
          FROM compras.CompraDetalle cd
          JOIN compras.OrdenCompraDetalle od ON od.IdOrdenCompra = c.IdOrdenCompra AND od.IdMaterial = cd.IdMaterial
          JOIN maestra.Proveedor p ON p.IdProveedor = od.IdProveedor
          WHERE cd.IdCompra = c.IdCompra) q
) proveedores

    
    WHERE (@Aceptada IS NULL OR c.Aceptada = @Aceptada)
    AND (@IdProveedor IS NULL OR EXISTS (
        SELECT 1 FROM compras.CompraDetalle cd
        JOIN compras.OrdenCompraDetalle od ON od.IdOrdenCompra = c.IdOrdenCompra AND od.IdMaterial = cd.IdMaterial
        WHERE cd.IdCompra = c.IdCompra AND od.IdProveedor = @IdProveedor))
    ORDER BY c.IdCompra DESC;
END;
GO

CREATE OR ALTER PROCEDURE [compras].[usp_Compra_Registrar]
    @NumeroCompra NVARCHAR(30),
    @IdOrdenCompra INT,
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

    IF EXISTS (SELECT IdMaterial FROM @Items GROUP BY IdMaterial HAVING COUNT(*) > 1)
        THROW 51073, 'No se permiten materiales repetidos en una Compra.', 1;

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
GO

CREATE OR ALTER PROCEDURE [compras].[usp_OrdenCompra_Actualizar]
(
    @IdOrdenCompra INT,
    @NumeroOrdenCompra NVARCHAR(50),
    @IdRequerimiento INT,
    @IdMoneda INT,
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

IF NOT EXISTS (SELECT 1 FROM maestra.Moneda WHERE IdMoneda = @IdMoneda AND Activo = 1)
    THROW 51070, 'La moneda no existe o está inactiva.', 1;
IF EXISTS (SELECT IdMaterial FROM @Items GROUP BY IdMaterial HAVING COUNT(*) > 1)
    THROW 51071, 'No se permiten materiales repetidos en una OC.', 1;

BEGIN TRANSACTION;

BEGIN TRY
        UPDATE
    compras.OrdenCompra
SET
    NumeroOrdenCompra = @NumeroOrdenCompra,
               IdRequerimiento = @IdRequerimiento,
               IdMoneda = @IdMoneda,
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
    oc.Total = ISNULL(t.Total, 0)
FROM
    compras.OrdenCompra oc
        OUTER APPLY
        (
    SELECT
                SUM(d.Cantidad * d.PrecioUnitario) AS Total
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
GO

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
GO

CREATE OR ALTER PROCEDURE [compras].[usp_OrdenCompra_CrearDesdeRequerimiento]
(
    @NumeroOrdenCompra NVARCHAR(50),
    @IdRequerimiento INT,
    @IdMoneda INT,
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

IF NOT EXISTS (SELECT 1 FROM maestra.Moneda WHERE IdMoneda = @IdMoneda AND Activo = 1)
    THROW 51070, 'La moneda no existe o está inactiva.', 1;
IF EXISTS (SELECT IdMaterial FROM @Items GROUP BY IdMaterial HAVING COUNT(*) > 1)
    THROW 51071, 'No se permiten materiales repetidos en una OC.', 1;

BEGIN TRANSACTION;

BEGIN TRY
        INSERT
    INTO
    compras.OrdenCompra
        (
            NumeroOrdenCompra,
    IdRequerimiento,
    IdMoneda,
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
@IdMoneda,
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
    oc.Total = ISNULL(t.Total, 0)
FROM
    compras.OrdenCompra oc
        OUTER APPLY
        (
    SELECT
                SUM(d.Cantidad * d.PrecioUnitario) AS Total
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
GO

CREATE OR ALTER PROCEDURE [compras].[usp_OrdenCompra_Get]
(
    @IdOrdenCompra INT
)
AS
BEGIN
    SET
NOCOUNT ON;

SELECT oc.IdOrdenCompra, oc.NumeroOrdenCompra, oc.IdRequerimiento,
       r.NumeroRequerimiento, r.IdProyecto, pr.NombreProyecto,
       oc.IdMoneda, mon.Codigo AS CodigoMoneda, mon.Simbolo AS SimboloMoneda,
       proveedores.Proveedores, especialidades.Especialidades,
       oc.FechaOrdenCompra, oc.Descripcion, oc.Estado, oc.Total,
       oc.RutaPdf, oc.FechaCreacion, oc.IdUsuarioCreacion
FROM compras.OrdenCompra oc
JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
JOIN maestra.Proyecto pr ON pr.IdProyecto = r.IdProyecto
JOIN maestra.Moneda mon ON mon.IdMoneda = oc.IdMoneda

OUTER APPLY (
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.RazonSocial), ', ') AS Proveedores
    FROM (SELECT DISTINCT p.IdProveedor, p.RazonSocial
          FROM compras.OrdenCompraDetalle d
          JOIN maestra.Proveedor p ON p.IdProveedor = d.IdProveedor
          WHERE d.IdOrdenCompra = oc.IdOrdenCompra) q
) proveedores
OUTER APPLY (
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.Nombre), ', ') AS Especialidades
    FROM (SELECT DISTINCT e.IdEspecialidad, e.Nombre
          FROM compras.OrdenCompraDetalle d
          JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
          JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
          WHERE d.IdOrdenCompra = oc.IdOrdenCompra) q
) especialidades
WHERE oc.IdOrdenCompra = @IdOrdenCompra;

SELECT
        d.IdOrdenCompraDetalle,
        d.IdOrdenCompra,
        d.IdMaterial,
        m.Descripcion AS Material,
        e.Nombre AS Especialidad,
        COALESCE(m.UnidadMedida, '-') AS UnidadMedida,
        d.Cantidad,
        d.IdProveedor AS IdProveedor,
        p.RazonSocial AS Proveedor,
        d.PrecioUnitario,
        d.Subtotal
FROM
    compras.OrdenCompraDetalle d
INNER JOIN compras.OrdenCompra oc ON
    oc.IdOrdenCompra = d.IdOrdenCompra
INNER JOIN maestra.Material m ON
    m.IdMaterial = d.IdMaterial
LEFT JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
LEFT JOIN maestra.Proveedor p ON
    p.IdProveedor = d.IdProveedor
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
GO

CREATE OR ALTER PROCEDURE compras.usp_OrdenCompra_List
    @Estado NVARCHAR(30) = NULL, @IdProveedor INT = NULL, @IdProyecto INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
SELECT oc.IdOrdenCompra, oc.NumeroOrdenCompra, oc.IdRequerimiento,
       r.NumeroRequerimiento, r.IdProyecto, pr.NombreProyecto,
       oc.IdMoneda, mon.Codigo AS CodigoMoneda, mon.Simbolo AS SimboloMoneda,
       proveedores.Proveedores, especialidades.Especialidades,
       oc.FechaOrdenCompra, oc.Descripcion, oc.Estado, oc.Total,
       oc.RutaPdf, oc.FechaCreacion, oc.IdUsuarioCreacion
FROM compras.OrdenCompra oc
JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
JOIN maestra.Proyecto pr ON pr.IdProyecto = r.IdProyecto
JOIN maestra.Moneda mon ON mon.IdMoneda = oc.IdMoneda

OUTER APPLY (
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.RazonSocial), ', ') AS Proveedores
    FROM (SELECT DISTINCT p.IdProveedor, p.RazonSocial
          FROM compras.OrdenCompraDetalle d
          JOIN maestra.Proveedor p ON p.IdProveedor = d.IdProveedor
          WHERE d.IdOrdenCompra = oc.IdOrdenCompra) q
) proveedores
OUTER APPLY (
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.Nombre), ', ') AS Especialidades
    FROM (SELECT DISTINCT e.IdEspecialidad, e.Nombre
          FROM compras.OrdenCompraDetalle d
          JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
          JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
          WHERE d.IdOrdenCompra = oc.IdOrdenCompra) q
) especialidades
WHERE (@Estado IS NULL OR @Estado = '' OR oc.Estado = @Estado)
  AND (@IdProyecto IS NULL OR r.IdProyecto = @IdProyecto)
  AND (@IdProveedor IS NULL OR EXISTS (SELECT 1 FROM compras.OrdenCompraDetalle d
       WHERE d.IdOrdenCompra = oc.IdOrdenCompra AND d.IdProveedor = @IdProveedor))
ORDER BY oc.IdOrdenCompra DESC;
END;
GO

CREATE OR ALTER PROCEDURE [compras].[usp_Requerimiento_Actualizar]
(
    @IdRequerimiento INT,
    @NumeroRequerimiento NVARCHAR(50),
    @FechaRequerimiento DATE,
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

IF NOT EXISTS (SELECT 1 FROM compras.Requerimiento WHERE IdRequerimiento = @IdRequerimiento)
    THROW 51072, 'El requerimiento no existe.', 1;
IF NOT EXISTS (SELECT 1 FROM @Items)
    THROW 50045, 'Debe registrar al menos un item.', 1;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
UPDATE
    compras.Requerimiento
SET
    NumeroRequerimiento = @NumeroRequerimiento,
           FechaRequerimiento = @FechaRequerimiento,
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
    Observacion,
    IdPresupuestoDetalle)
    SELECT
    @IdRequerimiento,
    i.IdMaterial,
    i.Cantidad,
    i.Observacion,
    i.IdPresupuestoDetalle
FROM
    @Items i;
COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
END;
GO


CREATE OR ALTER PROCEDURE [compras].[usp_Requerimiento_Crear]
    @NumeroRequerimiento NVARCHAR(30),
    @FechaRequerimiento DATE,
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
BEGIN TRY

INSERT
    INTO
    compras.Requerimiento
    (
        NumeroRequerimiento,
    FechaRequerimiento,
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
    Observacion,
    IdPresupuestoDetalle
    )
    SELECT
    @IdRequerimiento,
    IdMaterial,
    Cantidad,
    Observacion,
    IdPresupuestoDetalle
FROM
    @Items;

COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    @IdRequerimiento AS IdRequerimiento;
END;
GO

CREATE OR ALTER PROCEDURE [compras].[usp_Requerimiento_Get]
    @IdRequerimiento INT
AS
BEGIN
    SET
NOCOUNT ON;
SELECT r.IdRequerimiento, r.NumeroRequerimiento, r.FechaRequerimiento,
       r.IdProyecto, p.NombreProyecto, especialidades.Especialidades,
       r.Descripcion, r.FechaEntrega, r.IdUsuarioSolicitante,
       u.Nombres + ISNULL(N' ' + u.Apellidos, N'') AS Solicitante,
       r.Estado, r.Observacion, r.FechaCreacion
FROM compras.Requerimiento r
JOIN maestra.Proyecto p ON p.IdProyecto = r.IdProyecto
JOIN seguridad.Usuario u ON u.IdUsuario = r.IdUsuarioSolicitante

OUTER APPLY (
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.Nombre), ', ') AS Especialidades
    FROM (SELECT DISTINCT e.IdEspecialidad, e.Nombre
          FROM compras.RequerimientoDetalle rd
          JOIN maestra.Material m ON m.IdMaterial = rd.IdMaterial
          JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
          WHERE rd.IdRequerimiento = r.IdRequerimiento) q
) especialidades
WHERE r.IdRequerimiento = @IdRequerimiento;
    SELECT
    rd.IdRequerimientoDetalle,
            rd.IdRequerimiento,
            rd.IdMaterial,
            rd.IdPresupuestoDetalle,
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
GO

CREATE OR ALTER PROCEDURE compras.usp_Requerimiento_List
    @Estado NVARCHAR(30) = NULL, @IdEspecialidad INT = NULL, @IdProyecto INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
SELECT r.IdRequerimiento, r.NumeroRequerimiento, r.FechaRequerimiento,
       r.IdProyecto, p.NombreProyecto, especialidades.Especialidades,
       r.Descripcion, r.FechaEntrega, r.IdUsuarioSolicitante,
       u.Nombres + ISNULL(N' ' + u.Apellidos, N'') AS Solicitante,
       r.Estado, r.Observacion, r.FechaCreacion
FROM compras.Requerimiento r
JOIN maestra.Proyecto p ON p.IdProyecto = r.IdProyecto
JOIN seguridad.Usuario u ON u.IdUsuario = r.IdUsuarioSolicitante

OUTER APPLY (
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.Nombre), ', ') AS Especialidades
    FROM (SELECT DISTINCT e.IdEspecialidad, e.Nombre
          FROM compras.RequerimientoDetalle rd
          JOIN maestra.Material m ON m.IdMaterial = rd.IdMaterial
          JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
          WHERE rd.IdRequerimiento = r.IdRequerimiento) q
) especialidades
WHERE (@Estado IS NULL OR @Estado = '' OR r.Estado = @Estado)
  AND (@IdProyecto IS NULL OR r.IdProyecto = @IdProyecto)
  AND (@IdEspecialidad IS NULL OR EXISTS (
       SELECT 1 FROM compras.RequerimientoDetalle rd
       JOIN maestra.Material m ON m.IdMaterial = rd.IdMaterial
       WHERE rd.IdRequerimiento = r.IdRequerimiento AND m.IdEspecialidad = @IdEspecialidad))
ORDER BY r.IdRequerimiento DESC;
END;
GO
