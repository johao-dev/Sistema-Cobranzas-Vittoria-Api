CREATE OR ALTER PROCEDURE contable.usp_CotizacionMaterialesResumen_Listar
    @IdProyecto INT = NULL
AS
BEGIN
    SET
    NOCOUNT ON;
    
    SELECT
        IdProyecto,
        Proyecto,
        IdEspecialidad,
        Especialidad,
        Cotizacion,
        Facturado,
        Saldo
FROM
    contable.vw_CotizacionMaterialesResumenTodosProyectos
WHERE
    @IdProyecto IS NULL
    OR IdProyecto = @IdProyecto
ORDER BY
    Proyecto,
    Especialidad;
END;

CREATE OR ALTER PROCEDURE contable.usp_CotizacionMaterialesTotalPorProyecto_Listar
    @IdProyecto INT = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
        IdProyecto,
        Proyecto,
        CotizacionMateriales
FROM
    contable.vw_CotizacionMaterialesPorProyecto
WHERE
    @IdProyecto IS NULL
    OR IdProyecto = @IdProyecto
ORDER BY
    Proyecto;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_PresupuestoProyecto_Delete]
    @IdPresupuestoProyecto INT
AS
BEGIN
    SET
NOCOUNT ON;

UPDATE
    [contable].[PresupuestoProyecto]
SET
        Activo = 0,
        FechaActualizacion = SYSDATETIME()
WHERE
    IdPresupuestoProyecto = @IdPresupuestoProyecto;

UPDATE
    [contable].[PresupuestoProyectoDetalle]
SET
    Activo = 0
WHERE
    IdPresupuestoProyecto = @IdPresupuestoProyecto;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_PresupuestoProyecto_Get]
    @IdPresupuestoProyecto INT = NULL,
    @IdProyecto INT = NULL
AS
BEGIN
    SET
    NOCOUNT ON;
    
    SELECT
    TOP 1
        r.IdPresupuestoProyecto,
        r.IdProyecto,
        r.NombreProyecto,
        r.TotalPresupuesto,
        r.TotalCompras,
        r.SaldoRestante
FROM
    [contable].[vw_PresupuestoProyectoResumen] r
WHERE
    (@IdPresupuestoProyecto IS NULL
        OR r.IdPresupuestoProyecto = @IdPresupuestoProyecto)
    AND (@IdProyecto IS NULL
        OR r.IdProyecto = @IdProyecto);

SELECT
        d.IdPresupuestoProyectoDetalle,
        d.IdPresupuestoProyecto,
        d.Orden,
        d.Concepto,
        d.Soles,
        d.Dolares,
        d.Incidencia
FROM
    [contable].[PresupuestoProyectoDetalle] d
INNER JOIN [contable].[PresupuestoProyecto] p
        ON
    p.IdPresupuestoProyecto = d.IdPresupuestoProyecto
WHERE
    ISNULL(d.Activo, 1) = 1
    AND ISNULL(p.Activo, 1) = 1
    AND (@IdPresupuestoProyecto IS NULL
        OR d.IdPresupuestoProyecto = @IdPresupuestoProyecto)
    AND (@IdProyecto IS NULL
        OR p.IdProyecto = @IdProyecto)
ORDER BY
    d.Orden,
    d.IdPresupuestoProyectoDetalle;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_PresupuestoProyecto_List]
    @IdProyecto INT = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
        r.IdPresupuestoProyecto,
        r.IdProyecto,
        r.NombreProyecto,
        r.TotalPresupuesto,
        r.TotalCompras,
        r.SaldoRestante
FROM
    [contable].[vw_PresupuestoProyectoResumen] r
WHERE
    (@IdProyecto IS NULL
        OR r.IdProyecto = @IdProyecto)
ORDER BY
    r.NombreProyecto;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_PresupuestoProyecto_Upsert]
    @IdPresupuestoProyecto INT = NULL,
    @IdProyecto INT,
    @Detalles [contable].[TVP_PresupuestoProyectoDetalle] READONLY
AS
BEGIN
    SET
NOCOUNT ON;

DECLARE @IdCabecera INT;

SELECT
    @IdCabecera = IdPresupuestoProyecto
FROM
    [contable].[PresupuestoProyecto]
WHERE
    IdProyecto = @IdProyecto;

IF @IdCabecera IS NULL
    BEGIN
        INSERT
    INTO
    [contable].[PresupuestoProyecto]
        (
            IdProyecto,
            Activo,
            FechaCreacion
        )
VALUES
        (
            @IdProyecto,
            1,
            SYSDATETIME()
        );

SET
@IdCabecera = SCOPE_IDENTITY();
END
ELSE
BEGIN
        UPDATE
    [contable].[PresupuestoProyecto]
SET
            FechaActualizacion = SYSDATETIME(),
            Activo = 1
WHERE
    IdPresupuestoProyecto = @IdCabecera;
END

    DELETE
FROM
    [contable].[PresupuestoProyectoDetalle]
WHERE
    IdPresupuestoProyecto = @IdCabecera;

;

WITH DetallesOrdenados AS
    (
SELECT
            ROW_NUMBER() OVER (
    ORDER BY (
    SELECT
        1)) AS OrdenGenerado,
            d.Concepto,
            d.Soles,
            d.Incidencia
FROM
    @Detalles d
    )
    INSERT
    INTO
    [contable].[PresupuestoProyectoDetalle]
    (
        IdPresupuestoProyecto,
        Orden,
        Concepto,
        Soles,
        Dolares,
        Incidencia,
        Activo,
        FechaCreacion
    )
    SELECT
        @IdCabecera,
        OrdenGenerado,
        Concepto,
        Soles,
        CASE
        WHEN UPPER(LTRIM(RTRIM(Concepto))) = 'TERRENO' THEN ROUND(Soles / 3.41, 2)
        ELSE 0
    END,
        Incidencia,
        1,
        SYSDATETIME()
FROM
    DetallesOrdenados;

EXEC [contable].[usp_PresupuestoProyecto_Get] @IdPresupuestoProyecto = @IdCabecera;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_ProveedorReglaValorizacion_Upsert]
    @IdProveedor INT,
    @PorcentajeGarantia DECIMAL(9, 6),
    @PorcentajeDetraccion DECIMAL(9, 6),
    @Usuario NVARCHAR(100) = NULL
AS
BEGIN
    SET
NOCOUNT ON;

EXEC maestra.usp_ProveedorReglaValorizacion_Upsert
        @IdProveedor = @IdProveedor,
        @PorcentajeGarantia = @PorcentajeGarantia,
        @PorcentajeDetraccion = @PorcentajeDetraccion,
        @Usuario = @Usuario;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_Terreno_Delete]
    @IdTerreno INT
AS
BEGIN
    SET
NOCOUNT ON;

UPDATE
    [contable].[Terreno]
SET
        Activo = 0,
        FechaActualizacion = SYSDATETIME()
WHERE
    IdTerreno = @IdTerreno;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_Terreno_Get]
    @IdTerreno INT
AS
BEGIN
    SET
    NOCOUNT ON;
    
    SELECT
        t.IdTerreno,
        t.Fecha,
        t.IdProyecto,
        p.NombreProyecto,
        t.Terreno,
        t.Alcabala,
        t.Estado,
        t.Activo,
        t.FechaCreacion
FROM
    [contable].[Terreno] t
INNER JOIN [maestra].[Proyecto] p
        ON
    p.IdProyecto = t.IdProyecto
WHERE
    t.IdTerreno = @IdTerreno;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_Terreno_List]
    @IdProyecto INT = NULL,
    @Estado NVARCHAR(20) = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
        t.IdTerreno,
        t.Fecha,
        t.IdProyecto,
        p.NombreProyecto,
        t.Terreno,
        t.Alcabala,
        t.Estado,
        t.Activo,
        t.FechaCreacion
FROM
    [contable].[Terreno] t
INNER JOIN [maestra].[Proyecto] p
        ON
    p.IdProyecto = t.IdProyecto
WHERE
    t.Activo = 1
    AND (@IdProyecto IS NULL
        OR t.IdProyecto = @IdProyecto)
    AND (@Estado IS NULL
        OR t.Estado = @Estado)
ORDER BY
    t.IdTerreno DESC;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_Terreno_Upsert]
    @IdTerreno INT = NULL,
    @Fecha DATE = NULL,
    @IdProyecto INT,
    @Terreno NVARCHAR(250),
    @Alcabala DECIMAL(18, 2),
    @Estado NVARCHAR(20) = 'Activo'
AS
BEGIN
    SET
NOCOUNT ON;

IF @Fecha IS NULL
        SET
@Fecha = CAST(GETDATE() AS DATE);

IF @IdTerreno IS NULL
OR @IdTerreno = 0
    BEGIN
        INSERT
    INTO
    [contable].[Terreno]
        (
            Fecha,
            IdProyecto,
            Terreno,
            Alcabala,
            Estado,
            Activo,
            FechaCreacion
        )
VALUES
        (
            @Fecha,
            @IdProyecto,
            @Terreno,
            @Alcabala,
            @Estado,
            1,
            SYSDATETIME()
        );

SET
@IdTerreno = SCOPE_IDENTITY();
END
ELSE
BEGIN
        UPDATE
    [contable].[Terreno]
SET
            Fecha = @Fecha,
            IdProyecto = @IdProyecto,
            Terreno = @Terreno,
            Alcabala = @Alcabala,
            Estado = @Estado,
            FechaActualizacion = SYSDATETIME()
WHERE
    IdTerreno = @IdTerreno;
END

    EXEC [contable].[usp_Terreno_Get] @IdTerreno = @IdTerreno;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_Valorizacion_Get]
    @IdValorizacion INT
AS
BEGIN
    SET
    NOCOUNT ON;
    
    SELECT
        v.IdValorizacion,
        v.NumeroValorizacion,
        v.IdProyecto,
        p.NombreProyecto,
        v.IdProveedor,
        pr.RazonSocial AS Proveedor,
        v.IdEspecialidad,
        e.Nombre AS Especialidad,
        v.IdProveedorEspecialidadCotizacion,
        v.Empresa,
        v.Servicio,
        v.Moneda,
        v.Cotizacion,
        v.PorcentajeGarantia,
        v.PorcentajeDetraccion,
        v.Observacion
FROM
    contable.Valorizacion v
INNER JOIN maestra.Proveedor pr ON
    pr.IdProveedor = v.IdProveedor
INNER JOIN maestra.Especialidad e ON
    e.IdEspecialidad = v.IdEspecialidad
LEFT JOIN maestra.Proyecto p ON
    p.IdProyecto = v.IdProyecto
WHERE
    v.IdValorizacion = @IdValorizacion;

SELECT
        vd.IdValorizacionDetalle,
        vd.FechaFactura,
        vd.NumeroFactura,
        vd.MontoFactura,
        vd.Descripcion,
        CAST(vd.MontoFactura * ISNULL(vd.PorcentajeDetraccionAplicado, 0) AS DECIMAL(18, 2)) AS Detraccion,
        CAST(vd.MontoFactura * ISNULL(vd.PorcentajeGarantiaAplicado, 0) AS DECIMAL(18, 2)) AS Garantia,
        vd.MontoTransferido,
        vd.FechaTransferencia
FROM
    contable.ValorizacionDetalle vd
WHERE
    vd.IdValorizacion = @IdValorizacion
ORDER BY
    ISNULL(vd.FechaFactura, '19000101'),
    vd.IdValorizacionDetalle;

SELECT
        v.Cotizacion,
        ISNULL(SUM(vd.MontoFactura * ISNULL(vd.PorcentajeGarantiaAplicado, 0)), 0) AS GarantiaRetenida,
        ISNULL(SUM(vd.MontoFactura), 0) AS Facturado,
        ISNULL(SUM(vd.MontoTransferido), 0) AS Transferido,
        CAST(v.Cotizacion - ISNULL(SUM(vd.MontoFactura), 0) AS DECIMAL(18, 2)) AS Resta,
        CAST(ISNULL(SUM(vd.MontoFactura), 0) - ISNULL(SUM(vd.MontoTransferido), 0) AS DECIMAL(18, 2)) AS Liquidar
FROM
    contable.Valorizacion v
LEFT JOIN contable.ValorizacionDetalle vd ON
    vd.IdValorizacion = v.IdValorizacion
WHERE
    v.IdValorizacion = @IdValorizacion
GROUP BY
    v.Cotizacion;

SELECT
        a.IdValorizacionDetalleArchivo,
        a.IdValorizacionDetalle,
        a.NombreArchivo,
        a.RutaArchivo,
        a.Extension
FROM
    contable.ValorizacionDetalleArchivo a
INNER JOIN contable.ValorizacionDetalle vd ON
    vd.IdValorizacionDetalle = a.IdValorizacionDetalle
WHERE
    vd.IdValorizacion = @IdValorizacion
ORDER BY
    a.IdValorizacionDetalle,
    a.IdValorizacionDetalleArchivo;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_Valorizacion_GetById]
    @IdValorizacion INT
AS
BEGIN
    SET
NOCOUNT ON;

EXEC contable.usp_Valorizacion_Get
        @IdValorizacion = @IdValorizacion;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_Valorizacion_List]
    @IdProyecto INT = NULL,
    @IdProveedor INT = NULL,
    @IdEspecialidad INT = NULL
AS
BEGIN
    SET
NOCOUNT ON;

SELECT
        IdValorizacion,
        NumeroValorizacion,
        IdProyecto,
        NombreProyecto,
        IdProveedor,
        Proveedor,
        IdEspecialidad,
        Especialidad,
        Empresa,
        Servicio,
        Moneda,
        Cotizacion,
        PorcentajeGarantia,
        PorcentajeDetraccion,
        Facturado,
        Transferido,
        GarantiaRetenida,
        DetraccionAcumulada,
        OtrosDescuentos,
        Resta,
        Liquidar,
        AFavor,
        Deuda,
        FechaCreacion
FROM
    contable.vw_ValorizacionResumen
WHERE
    (@IdProyecto IS NULL
        OR IdProyecto = @IdProyecto)
    AND (@IdProveedor IS NULL
        OR IdProveedor = @IdProveedor)
    AND (@IdEspecialidad IS NULL
        OR IdEspecialidad = @IdEspecialidad)
ORDER BY
    FechaCreacion DESC,
    IdValorizacion DESC;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_Valorizacion_Upsert]
    @IdValorizacion INT = NULL,
    @NumeroValorizacion NVARCHAR(30),
    @IdProyecto INT = NULL,
    @IdProveedor INT,
    @IdEspecialidad INT,
    @IdProveedorEspecialidadCotizacion INT = NULL,
    @Empresa NVARCHAR(200) = NULL,
    @Servicio NVARCHAR(250) = NULL,
    @Moneda NVARCHAR(20) = 'Soles',
    @Cotizacion DECIMAL(18, 2),
    @PorcentajeGarantia DECIMAL(9, 6) = NULL,
    @PorcentajeDetraccion DECIMAL(9, 6) = NULL,
    @Observacion NVARCHAR(250) = NULL
AS
BEGIN
    SET
NOCOUNT ON;

IF @PorcentajeGarantia IS NULL
OR @PorcentajeDetraccion IS NULL
    BEGIN
        SELECT
            @PorcentajeGarantia = ISNULL(@PorcentajeGarantia, ISNULL(rv.PorcentajeGarantia, 0.050000)),
            @PorcentajeDetraccion = ISNULL(@PorcentajeDetraccion, ISNULL(rv.PorcentajeDetraccion, 0.040000))
FROM
    maestra.Proveedor p
LEFT JOIN maestra.ProveedorReglaValorizacion rv
            ON
    rv.IdProveedor = p.IdProveedor
    AND rv.Activo = 1
WHERE
    p.IdProveedor = @IdProveedor;
END

    IF @IdValorizacion IS NULL
OR @IdValorizacion = 0
    BEGIN
        DECLARE @IdExistente INT;

SELECT
    TOP 1 @IdExistente = IdValorizacion
FROM
    contable.Valorizacion
WHERE
    NumeroValorizacion = @NumeroValorizacion
    AND Activo = 1
ORDER BY
    IdValorizacion DESC;

IF @IdExistente IS NOT NULL
        BEGIN
            UPDATE
    contable.Valorizacion
SET
    IdProyecto = @IdProyecto,
                IdProveedor = @IdProveedor,
                IdEspecialidad = @IdEspecialidad,
                IdProveedorEspecialidadCotizacion = @IdProveedorEspecialidadCotizacion,
                Empresa = @Empresa,
                Servicio = @Servicio,
                Moneda = @Moneda,
                Cotizacion = @Cotizacion,
                PorcentajeGarantia = @PorcentajeGarantia,
                PorcentajeDetraccion = @PorcentajeDetraccion,
                Observacion = @Observacion
WHERE
    IdValorizacion = @IdExistente;

SELECT
                @IdExistente AS IdValorizacion,
                @NumeroValorizacion AS NumeroValorizacion,
                CAST(1 AS BIT) AS Reutilizada;

RETURN;
END

        INSERT
    INTO
    contable.Valorizacion
        (
            NumeroValorizacion,
            IdProyecto,
            IdProveedor,
            IdEspecialidad,
            IdProveedorEspecialidadCotizacion,
            Empresa,
            Servicio,
            Moneda,
            Cotizacion,
            PorcentajeGarantia,
            PorcentajeDetraccion,
            Observacion
        )
VALUES
        (
            @NumeroValorizacion,
            @IdProyecto,
            @IdProveedor,
            @IdEspecialidad,
            @IdProveedorEspecialidadCotizacion,
            @Empresa,
            @Servicio,
            @Moneda,
            @Cotizacion,
            @PorcentajeGarantia,
            @PorcentajeDetraccion,
            @Observacion
        );

SELECT
            SCOPE_IDENTITY() AS IdValorizacion,
            @NumeroValorizacion AS NumeroValorizacion,
            CAST(0 AS BIT) AS Reutilizada;
END
ELSE
    BEGIN
        IF EXISTS (
SELECT
1
FROM
contable.Valorizacion
WHERE
NumeroValorizacion = @NumeroValorizacion
AND IdValorizacion <> @IdValorizacion
AND Activo = 1
        )
        BEGIN
            RAISERROR('Ya existe otra valorización activa con ese número.', 16, 1);

RETURN;
END

        UPDATE
    contable.Valorizacion
SET
    NumeroValorizacion = @NumeroValorizacion,
            IdProyecto = @IdProyecto,
            IdProveedor = @IdProveedor,
            IdEspecialidad = @IdEspecialidad,
            IdProveedorEspecialidadCotizacion = @IdProveedorEspecialidadCotizacion,
            Empresa = @Empresa,
            Servicio = @Servicio,
            Moneda = @Moneda,
            Cotizacion = @Cotizacion,
            PorcentajeGarantia = @PorcentajeGarantia,
            PorcentajeDetraccion = @PorcentajeDetraccion,
            Observacion = @Observacion
WHERE
    IdValorizacion = @IdValorizacion;

SELECT
            @IdValorizacion AS IdValorizacion,
            @NumeroValorizacion AS NumeroValorizacion,
            CAST(0 AS BIT) AS Reutilizada;
END
END;

CREATE OR ALTER PROCEDURE [contable].[usp_ValorizacionConfiguracion_List]
    @IdProyecto INT = NULL,
    @IdProveedor INT = NULL,
    @IdEspecialidad INT = NULL
AS
BEGIN
    SET
NOCOUNT ON;

EXEC maestra.usp_ProveedorEspecialidadCotizacion_List
        @IdProyecto = @IdProyecto,
        @IdProveedor = @IdProveedor,
        @IdEspecialidad = @IdEspecialidad;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_ValorizacionConfiguracion_Upsert]
    @IdConfiguracion INT = NULL,
    @IdProyecto INT = NULL,
    @IdProveedor INT,
    @IdEspecialidad INT,
    @Servicio NVARCHAR(250) = NULL,
    @Moneda NVARCHAR(20) = 'Soles',
    @MontoCotizacion DECIMAL(18, 2),
    @Usuario NVARCHAR(100) = NULL
AS
BEGIN
    SET
NOCOUNT ON;

EXEC maestra.usp_ProveedorEspecialidadCotizacion_Upsert
        @IdProveedorEspecialidadCotizacion = @IdConfiguracion,
        @IdProyecto = @IdProyecto,
        @IdProveedor = @IdProveedor,
        @IdEspecialidad = @IdEspecialidad,
        @Servicio = @Servicio,
        @Moneda = @Moneda,
        @MontoCotizacion = @MontoCotizacion,
        @Activo = 1;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_ValorizacionDetalle_Delete]
    @IdValorizacionDetalle INT
AS
BEGIN
    SET
NOCOUNT ON;

DELETE
FROM
    contable.ValorizacionDetalleArchivo
WHERE
    IdValorizacionDetalle = @IdValorizacionDetalle;

DELETE
FROM
    contable.ValorizacionDetalle
WHERE
    IdValorizacionDetalle = @IdValorizacionDetalle;
END;

CREATE OR ALTER PROCEDURE [contable].[usp_ValorizacionDetalle_Upsert]
    @IdValorizacionDetalle INT = NULL,
    @IdValorizacion INT,
    @FechaFactura DATE = NULL,
    @NumeroFactura NVARCHAR(50) = NULL,
    @MontoFactura DECIMAL(18, 2),
    @Descripcion NVARCHAR(500) = NULL,
    @OtrosDescuentos DECIMAL(18, 2) = 0,
    @FechaTransferencia DATE = NULL,
    @NumeroOperacion NVARCHAR(50) = NULL,
    @BancoTransferencia NVARCHAR(100) = NULL,
    @BancoDestino NVARCHAR(100) = NULL,
    @MontoTransferido DECIMAL(18, 2) = 0,
    @PorcentajeDetraccionAplicado DECIMAL(9, 6) = NULL,
    @PorcentajeGarantiaAplicado DECIMAL(9, 6) = NULL
AS
BEGIN
    SET
    NOCOUNT ON;
    
    IF @PorcentajeDetraccionAplicado IS NULL
    OR @PorcentajeGarantiaAplicado IS NULL
BEGIN
        SELECT
            @PorcentajeDetraccionAplicado = ISNULL(@PorcentajeDetraccionAplicado, PorcentajeDetraccion),
            @PorcentajeGarantiaAplicado = ISNULL(@PorcentajeGarantiaAplicado, PorcentajeGarantia)
FROM
    contable.Valorizacion
WHERE
    IdValorizacion = @IdValorizacion;
END

    IF @IdValorizacionDetalle IS NULL
OR @IdValorizacionDetalle = 0
    BEGIN
        INSERT
    INTO
    contable.ValorizacionDetalle
        (
            IdValorizacion,
            FechaFactura,
            NumeroFactura,
            MontoFactura,
            Descripcion,
            PorcentajeDetraccionAplicado,
            PorcentajeGarantiaAplicado,
            OtrosDescuentos,
            FechaTransferencia,
            NumeroOperacion,
            BancoTransferencia,
            BancoDestino,
            MontoTransferido
        )
VALUES
        (
            @IdValorizacion,
            @FechaFactura,
            @NumeroFactura,
            @MontoFactura,
            @Descripcion,
            @PorcentajeDetraccionAplicado,
            @PorcentajeGarantiaAplicado,
            @OtrosDescuentos,
            @FechaTransferencia,
            @NumeroOperacion,
            @BancoTransferencia,
            @BancoDestino,
            @MontoTransferido
        );

SELECT
    SCOPE_IDENTITY() AS IdValorizacionDetalle;
END
ELSE
    BEGIN
        UPDATE
    contable.ValorizacionDetalle
SET
    FechaFactura = @FechaFactura,
            NumeroFactura = @NumeroFactura,
            MontoFactura = @MontoFactura,
            Descripcion = @Descripcion,
            PorcentajeDetraccionAplicado = @PorcentajeDetraccionAplicado,
            PorcentajeGarantiaAplicado = @PorcentajeGarantiaAplicado,
            OtrosDescuentos = @OtrosDescuentos,
            FechaTransferencia = @FechaTransferencia,
            NumeroOperacion = @NumeroOperacion,
            BancoTransferencia = @BancoTransferencia,
            BancoDestino = @BancoDestino,
            MontoTransferido = @MontoTransferido
WHERE
    IdValorizacionDetalle = @IdValorizacionDetalle;

SELECT
    @IdValorizacionDetalle AS IdValorizacionDetalle;
END
END;
