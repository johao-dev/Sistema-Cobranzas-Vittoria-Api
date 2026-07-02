CREATE OR ALTER PROCEDURE [almacen].[usp_Kardex_List]
    @IdMaterial INT = NULL,
    @IdEspecialidad INT = NULL,
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        km.IdKardexMovimiento,
        km.IdMaterial,
        m.Descripcion AS Material,
        km.IdEspecialidad,
        e.Nombre AS Especialidad,
        km.TipoMovimiento,
        km.FechaMovimiento,
        km.CantidadEntrada,
        km.CantidadSalida,
        km.StockResultante,
        km.IdCompra,
        km.IdOrdenCompra,
        km.Observacion,
        km.FechaIngresoAlmacen,
        km.FechaSalidaAlmacen,
        km.FechaCreacion
    FROM almacen.KardexMovimiento km
    INNER JOIN maestra.Material m ON m.IdMaterial = km.IdMaterial
    INNER JOIN maestra.Especialidad e ON e.IdEspecialidad = km.IdEspecialidad

    WHERE (@IdMaterial IS NULL OR km.IdMaterial = @IdMaterial)
    AND (@IdEspecialidad IS NULL OR km.IdEspecialidad = @IdEspecialidad)
    AND (@FechaDesde IS NULL OR km.FechaMovimiento >= @FechaDesde)
    AND (@FechaHasta IS NULL OR km.FechaMovimiento <= @FechaHasta)

    ORDER BY
        km.FechaMovimiento DESC,
        km.IdKardexMovimiento DESC;
END;


CREATE OR ALTER PROCEDURE [almacen].[usp_Kardex_RegistrarSalida]
    @IdCompra INT,
    @IdMaterial INT,
    @IdEspecialidad INT = NULL,
    @FechaMovimiento DATE,
    @CantidadSalida DECIMAL(18, 2),
    @Observacion NVARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @CantidadSalida IS NULL OR @CantidadSalida <= 0
        THROW 51001, 'La cantidad de salida debe ser mayor a cero.',
    1;

    IF NOT EXISTS (
        SELECT
            1
        FROM compras.CompraDetalle cd
        
        WHERE cd.IdCompra = @IdCompra
        AND cd.IdMaterial = @IdMaterial
    )
        THROW 51004, 'El material seleccionado no pertenece a la compra elegida.',
    1;

    IF @IdEspecialidad IS NULL
        SELECT @IdEspecialidad = IdEspecialidad
        FROM maestra.Material
        
        WHERE
            IdMaterial = @IdMaterial;

    IF @IdEspecialidad IS NULL
        THROW 51002, 'No se pudo determinar la especialidad del material.',
    1;

    DECLARE @StockActual DECIMAL(18, 2);
    ;

    WITH
    EntradasCompra AS (
        SELECT
            CAST(ISNULL(cd.Cantidad, 0) AS DECIMAL(18, 2)) AS Entrada,
            CAST(0 AS DECIMAL(18, 2)) AS Salida
        
        FROM compras.CompraDetalle cd
        INNER JOIN maestra.Material m ON m.IdMaterial = cd.IdMaterial
        
        WHERE cd.IdCompra = @IdCompra
        AND cd.IdMaterial = @IdMaterial
        AND m.IdEspecialidad = @IdEspecialidad
    ),

    SalidasManual AS (
        SELECT
            CAST(0 AS DECIMAL(18, 2)) AS Entrada,
            CAST(ISNULL(km.CantidadSalida, 0) AS DECIMAL(18, 2)) AS Salida
    
        FROM almacen.KardexMovimiento km

        WHERE km.TipoMovimiento = 'SALIDA'
        AND km.IdCompra = @IdCompra
        AND km.IdMaterial = @IdMaterial
        AND km.IdEspecialidad = @IdEspecialidad
    ),

    Movs AS (
        SELECT
            *
        FROM EntradasCompra
        UNION ALL
        SELECT
            *
        FROM SalidasManual
    )

    SELECT @StockActual = ISNULL(SUM(Entrada - Salida), 0)
    FROM Movs;

    IF ISNULL(@StockActual, 0) < @CantidadSalida
        THROW 51003, 'La salida supera el stock disponible para esa compra.',
    1;

    INSERT INTO almacen.KardexMovimiento (
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
    ) VALUES (
        @IdMaterial,
        @IdEspecialidad,
        'SALIDA',
        @FechaMovimiento,
        0,
        @CantidadSalida,
        @StockActual - @CantidadSalida,
        @IdCompra,
        NULL,
        ISNULL(@Observacion, 'Salida manual de almacén'),
        NULL,
        @FechaMovimiento,
        GETDATE()
    );

    SELECT
        CAST(@StockActual - @CantidadSalida AS DECIMAL(18, 2)) AS StockActual,
        N'Salida registrada correctamente.' AS Mensaje;
END;


CREATE OR ALTER PROCEDURE [almacen].[usp_Kardex_ResumenMaterial]
    @IdMaterial INT = NULL,
    @IdEspecialidad INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    ;

    WITH
    Resumen AS (
        SELECT
            km.IdMaterial,
            km.IdEspecialidad,
            SUM(km.CantidadEntrada) AS TotalEntradas,
            SUM(km.CantidadSalida) AS TotalSalidas,
            MAX(km.IdKardexMovimiento) AS UltimoMovimiento
        
        FROM almacen.KardexMovimiento km

        WHERE (@IdMaterial IS NULL OR km.IdMaterial = @IdMaterial)
        AND (@IdEspecialidad IS NULL OR km.IdEspecialidad = @IdEspecialidad)
        
        GROUP BY
            km.IdMaterial,
            km.IdEspecialidad
    )

    SELECT
        r.IdMaterial,
        m.Descripcion AS Material,
        r.IdEspecialidad,
        e.Nombre AS Especialidad,
        m.UnidadMedida,
        r.TotalEntradas,
        r.TotalSalidas,
        km.StockResultante AS StockActual
    
    FROM Resumen r
    INNER JOIN maestra.Material m ON m.IdMaterial = r.IdMaterial
    INNER JOIN maestra.Especialidad e ON e.IdEspecialidad = r.IdEspecialidad
    INNER JOIN almacen.KardexMovimiento km ON km.IdKardexMovimiento = r.UltimoMovimiento

    ORDER BY
        e.Nombre,
        m.Descripcion;
END;
