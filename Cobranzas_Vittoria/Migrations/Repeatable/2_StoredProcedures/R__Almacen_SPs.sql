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
GO

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
GO

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
GO


-- =============================================================================
-- Kardex manual: stock global único por material. La especialidad se deriva de
-- maestra.Material tanto en respuestas como en filtros, por cada detalle de salida.
-- Los rechazos del módulo usan el rango 51100-51199 y mensajes CODIGO: detalle.
-- =============================================================================

CREATE OR ALTER PROCEDURE almacen.usp_KardexEntrada_Listar
    @IdEspecialidad INT = NULL,
    @IdProyecto INT = NULL,
    @IdProveedor INT = NULL,
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ke.IdKardexEntrada, m.IdEspecialidad, e.Nombre AS Especialidad,
        ke.IdMaterial, m.Codigo AS CodigoMaterial, m.Descripcion AS Nombre,
        ke.IdProveedor, p.RazonSocial AS Proveedor, ke.IdProyecto,
        pr.NombreProyecto AS Proyecto, ke.NumeroDocumento, ke.Fecha,
        ke.Cantidad, ke.Observacion, ke.FechaCreacion
    FROM almacen.KardexEntrada ke
    JOIN maestra.Material m ON m.IdMaterial = ke.IdMaterial
    JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
    LEFT JOIN maestra.Proveedor p ON p.IdProveedor = ke.IdProveedor
    LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = ke.IdProyecto
    WHERE (@IdEspecialidad IS NULL OR m.IdEspecialidad = @IdEspecialidad)
      AND (@IdProyecto IS NULL OR ke.IdProyecto = @IdProyecto)
      AND (@IdProveedor IS NULL OR ke.IdProveedor = @IdProveedor)
      AND (@FechaDesde IS NULL OR ke.Fecha >= @FechaDesde)
      AND (@FechaHasta IS NULL OR ke.Fecha <= @FechaHasta)
    ORDER BY ke.Fecha DESC, ke.IdKardexEntrada DESC;
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_KardexEntrada_Registrar
    @IdMaterial INT,
    @IdProveedor INT = NULL,
    @IdProyecto INT = NULL,
    @NumeroDocumento NVARCHAR(50) = NULL,
    @Fecha DATE,
    @Cantidad DECIMAL(18,2),
    @Observacion NVARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Serializar escrituras del Kardex manual, incluidas altas sin fila de stock.
        DECLARE @Bloqueo INT;
        EXEC @Bloqueo = sys.sp_getapplock
            @Resource = N'Inventario:Movimientos', @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 10000;
        IF @Bloqueo < 0
            THROW 51113, 'INVENTARIO_OCUPADO: No se pudo obtener el bloqueo de inventario. Reintente la operación.', 1;

        EXEC almacen.usp_StockProyecto_ExigirInicializacion;

        IF @IdMaterial IS NULL OR @Fecha IS NULL OR @Cantidad IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idMaterial, fecha y cantidad son obligatorios.', 1;
        IF @Cantidad < 0
            THROW 51103, 'CANTIDAD_INVALIDA: La cantidad no puede ser negativa.', 1;

        -- HOLDLOCK coordina el alta de movimientos con la reclasificación de Material.
        IF NOT EXISTS (SELECT 1 FROM maestra.Material WITH (HOLDLOCK) WHERE IdMaterial = @IdMaterial)
            THROW 51101, 'FK_NO_EXISTE: idMaterial', 1;
        IF @IdProveedor IS NOT NULL AND NOT EXISTS (SELECT 1 FROM maestra.Proveedor WHERE IdProveedor = @IdProveedor)
            THROW 51101, 'FK_NO_EXISTE: idProveedor', 1;
        IF @IdProyecto IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idProyecto', 1;
        IF NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = @IdProyecto)
            THROW 51101, 'FK_NO_EXISTE: idProyecto', 1;

        INSERT INTO almacen.KardexEntrada
            (IdMaterial, IdProveedor, IdProyecto, NumeroDocumento, Fecha, Cantidad, Observacion)
        VALUES (@IdMaterial, @IdProveedor, @IdProyecto, @NumeroDocumento, @Fecha, @Cantidad, @Observacion);
        DECLARE @NewId INT = CONVERT(INT, SCOPE_IDENTITY());

        UPDATE almacen.KardexStock
        SET TotalEntrada = TotalEntrada + @Cantidad,
            Stock = Stock + @Cantidad, FechaUltimaMovimiento = @Fecha
        WHERE IdMaterial = @IdMaterial;
        IF @@ROWCOUNT = 0
            INSERT INTO almacen.KardexStock (IdMaterial, TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento)
            VALUES (@IdMaterial, @Cantidad, 0, @Cantidad, @Fecha);

        DECLARE @Cambios almacen.TVP_StockProyectoCantidad;
        INSERT INTO @Cambios VALUES (@IdMaterial, @IdProyecto, @Cantidad);
        EXEC almacen.usp_StockProyecto_AplicarCambios @Cambios;

        COMMIT;
    SELECT ke.IdKardexEntrada, m.IdEspecialidad, e.Nombre AS Especialidad,
        ke.IdMaterial, m.Codigo AS CodigoMaterial, m.Descripcion AS Nombre,
        ke.IdProveedor, p.RazonSocial AS Proveedor, ke.IdProyecto,
        pr.NombreProyecto AS Proyecto, ke.NumeroDocumento, ke.Fecha,
        ke.Cantidad, ke.Observacion, ke.FechaCreacion
    FROM almacen.KardexEntrada ke
    JOIN maestra.Material m ON m.IdMaterial = ke.IdMaterial
    JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
    LEFT JOIN maestra.Proveedor p ON p.IdProveedor = ke.IdProveedor
    LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = ke.IdProyecto
    WHERE ke.IdKardexEntrada = @NewId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_KardexEntrada_Actualizar
    @IdKardexEntrada INT,
    @IdMaterial INT,
    @IdProveedor INT = NULL,
    @IdProyecto INT = NULL,
    @NumeroDocumento NVARCHAR(50) = NULL,
    @Fecha DATE,
    @Cantidad DECIMAL(18,2),
    @Observacion NVARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Serializar escrituras del Kardex manual, incluidas altas sin fila de stock.
        DECLARE @Bloqueo INT;
        EXEC @Bloqueo = sys.sp_getapplock
            @Resource = N'Inventario:Movimientos', @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 10000;
        IF @Bloqueo < 0
            THROW 51113, 'INVENTARIO_OCUPADO: No se pudo obtener el bloqueo de inventario. Reintente la operación.', 1;

        EXEC almacen.usp_StockProyecto_ExigirInicializacion;

        IF @IdMaterial IS NULL OR @Fecha IS NULL OR @Cantidad IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idMaterial, fecha y cantidad son obligatorios.', 1;
        IF @Cantidad < 0
            THROW 51103, 'CANTIDAD_INVALIDA: La cantidad no puede ser negativa.', 1;

        -- HOLDLOCK coordina el alta de movimientos con la reclasificación de Material.
        IF NOT EXISTS (SELECT 1 FROM maestra.Material WITH (HOLDLOCK) WHERE IdMaterial = @IdMaterial)
            THROW 51101, 'FK_NO_EXISTE: idMaterial', 1;
        IF @IdProveedor IS NOT NULL AND NOT EXISTS (SELECT 1 FROM maestra.Proveedor WHERE IdProveedor = @IdProveedor)
            THROW 51101, 'FK_NO_EXISTE: idProveedor', 1;
        IF @IdProyecto IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idProyecto', 1;
        IF NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = @IdProyecto)
            THROW 51101, 'FK_NO_EXISTE: idProyecto', 1;

        DECLARE @MaterialAnterior INT, @ProyectoAnterior INT, @CantidadAnterior DECIMAL(18,2);
        SELECT @MaterialAnterior = IdMaterial, @ProyectoAnterior = IdProyecto, @CantidadAnterior = Cantidad
        FROM almacen.KardexEntrada WHERE IdKardexEntrada = @IdKardexEntrada;
        IF @MaterialAnterior IS NULL
            THROW 51104, 'KARDEX_NO_ENCONTRADO: idKardexEntrada', 1;

        DECLARE @Delta DECIMAL(19,2) = CASE WHEN @MaterialAnterior = @IdMaterial
            THEN @Cantidad - @CantidadAnterior ELSE -@CantidadAnterior END;
        IF ISNULL((SELECT Stock FROM almacen.KardexStock WHERE IdMaterial = @MaterialAnterior), 0) + @Delta < 0
            THROW 51111, 'STOCK_INCONSISTENTE_AL_ELIMINAR: La modificación dejaría stock negativo.', 1;

        IF @ProyectoAnterior IS NULL
            THROW 51119, 'PROYECTO_MOVIMIENTO_REQUERIDO: Regularice el proyecto del movimiento histórico antes de modificarlo.', 1;

        DECLARE @Cambios almacen.TVP_StockProyectoCantidad;
        INSERT INTO @Cambios
        SELECT IdMaterial, IdProyecto, SUM(Cantidad)
        FROM (VALUES (@MaterialAnterior, @ProyectoAnterior, -CAST(@CantidadAnterior AS DECIMAL(29,2))),
                     (@IdMaterial, @IdProyecto, CAST(@Cantidad AS DECIMAL(29,2)))) c(IdMaterial, IdProyecto, Cantidad)
        GROUP BY IdMaterial, IdProyecto;
        EXEC almacen.usp_StockProyecto_ValidarCambios @Cambios;

        UPDATE almacen.KardexStock
        SET TotalEntrada = TotalEntrada + @Delta,
            Stock = Stock + @Delta, FechaUltimaMovimiento = @Fecha
        WHERE IdMaterial = @MaterialAnterior;

        IF @@ROWCOUNT = 0 AND @MaterialAnterior = @IdMaterial
            INSERT INTO almacen.KardexStock (IdMaterial, TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento)
            VALUES (@IdMaterial, @Cantidad, 0, @Cantidad, @Fecha);

        IF @MaterialAnterior <> @IdMaterial
        BEGIN
        UPDATE almacen.KardexStock
        SET TotalEntrada = TotalEntrada + @Cantidad,
            Stock = Stock + @Cantidad, FechaUltimaMovimiento = @Fecha
        WHERE IdMaterial = @IdMaterial;
        IF @@ROWCOUNT = 0
            INSERT INTO almacen.KardexStock (IdMaterial, TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento)
            VALUES (@IdMaterial, @Cantidad, 0, @Cantidad, @Fecha);
        END;

        UPDATE almacen.KardexEntrada
        SET IdMaterial = @IdMaterial, IdProveedor = @IdProveedor, IdProyecto = @IdProyecto,
            NumeroDocumento = @NumeroDocumento, Fecha = @Fecha, Cantidad = @Cantidad, Observacion = @Observacion
        WHERE IdKardexEntrada = @IdKardexEntrada;

        EXEC almacen.usp_StockProyecto_AplicarCambios @Cambios;

        COMMIT;
    SELECT ke.IdKardexEntrada, m.IdEspecialidad, e.Nombre AS Especialidad,
        ke.IdMaterial, m.Codigo AS CodigoMaterial, m.Descripcion AS Nombre,
        ke.IdProveedor, p.RazonSocial AS Proveedor, ke.IdProyecto,
        pr.NombreProyecto AS Proyecto, ke.NumeroDocumento, ke.Fecha,
        ke.Cantidad, ke.Observacion, ke.FechaCreacion
    FROM almacen.KardexEntrada ke
    JOIN maestra.Material m ON m.IdMaterial = ke.IdMaterial
    JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
    LEFT JOIN maestra.Proveedor p ON p.IdProveedor = ke.IdProveedor
    LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = ke.IdProyecto
    WHERE ke.IdKardexEntrada = @IdKardexEntrada;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_KardexEntrada_Eliminar
    @IdKardexEntrada INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Serializar escrituras del Kardex manual, incluidas altas sin fila de stock.
        DECLARE @Bloqueo INT;
        EXEC @Bloqueo = sys.sp_getapplock
            @Resource = N'Inventario:Movimientos', @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 10000;
        IF @Bloqueo < 0
            THROW 51113, 'INVENTARIO_OCUPADO: No se pudo obtener el bloqueo de inventario. Reintente la operación.', 1;

        EXEC almacen.usp_StockProyecto_ExigirInicializacion;

        DECLARE @IdMaterial INT, @IdProyecto INT, @Cantidad DECIMAL(18,2);
        SELECT @IdMaterial = IdMaterial, @IdProyecto = IdProyecto, @Cantidad = Cantidad
        FROM almacen.KardexEntrada WHERE IdKardexEntrada = @IdKardexEntrada;
        IF @IdMaterial IS NULL
            THROW 51104, 'KARDEX_NO_ENCONTRADO: idKardexEntrada', 1;
        IF ISNULL((SELECT Stock FROM almacen.KardexStock WHERE IdMaterial = @IdMaterial), 0) < @Cantidad
            THROW 51111, 'STOCK_INCONSISTENTE_AL_ELIMINAR: Hay salidas que dependen de esta entrada.', 1;

        IF @IdProyecto IS NULL
            THROW 51119, 'PROYECTO_MOVIMIENTO_REQUERIDO: Regularice el proyecto del movimiento histórico antes de eliminarlo.', 1;

        DECLARE @Cambios almacen.TVP_StockProyectoCantidad;
        INSERT INTO @Cambios VALUES (@IdMaterial, @IdProyecto, -@Cantidad);
        EXEC almacen.usp_StockProyecto_ValidarCambios @Cambios;

        UPDATE almacen.KardexStock
        SET TotalEntrada = TotalEntrada - @Cantidad, Stock = Stock - @Cantidad
        WHERE IdMaterial = @IdMaterial;
        DELETE FROM almacen.KardexEntrada WHERE IdKardexEntrada = @IdKardexEntrada;

        EXEC almacen.usp_StockProyecto_AplicarCambios @Cambios;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_KardexSalida_Listar
    @IdEspecialidad INT = NULL,
    @IdProyecto INT = NULL,
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ks.IdKardexSalida, m.IdEspecialidad, e.Nombre AS Especialidad,
        ks.IdProyecto, pr.NombreProyecto AS Proyecto, ks.NumeroDocumento,
        ks.Fecha, ks.Solicitante, ks.Observacion, d.IdKardexSalidaDetalle,
        d.IdMaterial, m.Codigo AS CodigoMaterial, m.Descripcion AS Nombre,
        d.Cantidad, d.Observacion AS DetalleObservacion, ks.FechaCreacion
    FROM almacen.KardexSalida ks
    JOIN almacen.KardexSalidaDetalle d ON d.IdKardexSalida = ks.IdKardexSalida
    JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
    JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
    LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = ks.IdProyecto
    WHERE (@IdEspecialidad IS NULL OR m.IdEspecialidad = @IdEspecialidad)
      AND (@IdProyecto IS NULL OR ks.IdProyecto = @IdProyecto)
      AND (@FechaDesde IS NULL OR ks.Fecha >= @FechaDesde)
      AND (@FechaHasta IS NULL OR ks.Fecha <= @FechaHasta)
    ORDER BY ks.Fecha DESC, ks.IdKardexSalida DESC, d.IdKardexSalidaDetalle;
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_KardexSalida_Registrar
    @IdProyecto INT = NULL,
    @NumeroDocumento NVARCHAR(50) = NULL,
    @Fecha DATE,
    @Solicitante NVARCHAR(150),
    @Observacion NVARCHAR(250) = NULL,
    @Items almacen.TVP_KardexSalidaItem READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Serializar escrituras del Kardex manual, incluidas altas sin fila de stock.
        DECLARE @Bloqueo INT;
        EXEC @Bloqueo = sys.sp_getapplock
            @Resource = N'Inventario:Movimientos', @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 10000;
        IF @Bloqueo < 0
            THROW 51113, 'INVENTARIO_OCUPADO: No se pudo obtener el bloqueo de inventario. Reintente la operación.', 1;

        EXEC almacen.usp_StockProyecto_ExigirInicializacion;

        IF @Fecha IS NULL OR @Solicitante IS NULL OR LTRIM(RTRIM(@Solicitante)) = ''
            THROW 51100, 'CAMPO_REQUERIDO: fecha y solicitante son obligatorios.', 1;
        IF @IdProyecto IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idProyecto', 1;
        IF NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = @IdProyecto)
            THROW 51101, 'FK_NO_EXISTE: idProyecto', 1;
        IF NOT EXISTS (SELECT 1 FROM @Items)
            THROW 51100, 'CAMPO_REQUERIDO: items (debe tener al menos uno)', 1;
        IF EXISTS (SELECT 1 FROM @Items WHERE IdMaterial IS NULL OR Cantidad IS NULL OR Cantidad < 0)
            THROW 51103, 'CANTIDAD_INVALIDA: Los ítems requieren material y cantidad no negativa.', 1;
        IF EXISTS (
            SELECT 1 FROM @Items i
            LEFT JOIN maestra.Material m WITH (HOLDLOCK) ON m.IdMaterial = i.IdMaterial
            WHERE m.IdMaterial IS NULL
        )
            THROW 51101, 'FK_NO_EXISTE: items contiene un material inexistente.', 1;

        -- Agrupar antes de validar: un material puede repetirse en varios detalles.
        DECLARE @Nuevos TABLE (IdMaterial INT PRIMARY KEY, Cantidad DECIMAL(28,2));
        INSERT INTO @Nuevos SELECT IdMaterial, SUM(Cantidad) FROM @Items GROUP BY IdMaterial;

        IF EXISTS (
            SELECT 1 FROM @Nuevos n LEFT JOIN almacen.KardexStock ks ON ks.IdMaterial = n.IdMaterial
            WHERE n.Cantidad > ISNULL(ks.Stock, 0)
        )
        BEGIN
            DECLARE @Detalle NVARCHAR(MAX);
            SELECT @Detalle = STRING_AGG(CONVERT(NVARCHAR(MAX), CONCAT(
                'idMaterial=', n.IdMaterial, ' disponible=', ISNULL(ks.Stock, 0), ' solicitado=', n.Cantidad)), N'; ')
            FROM @Nuevos n LEFT JOIN almacen.KardexStock ks ON ks.IdMaterial = n.IdMaterial
            WHERE n.Cantidad > ISNULL(ks.Stock, 0);
            DECLARE @Mensaje NVARCHAR(2048) = LEFT(CONCAT('STOCK_INSUFICIENTE: ', @Detalle), 2048);
            THROW 51110, @Mensaje, 1;
        END;

        DECLARE @Cambios almacen.TVP_StockProyectoCantidad;
        INSERT INTO @Cambios SELECT IdMaterial, @IdProyecto, -Cantidad FROM @Nuevos;
        EXEC almacen.usp_StockProyecto_ValidarCambios @Cambios;

        INSERT INTO almacen.KardexSalida (IdProyecto, NumeroDocumento, Fecha, Solicitante, Observacion)
        VALUES (@IdProyecto, @NumeroDocumento, @Fecha, @Solicitante, @Observacion);
        DECLARE @NewId INT = CONVERT(INT, SCOPE_IDENTITY());
        INSERT INTO almacen.KardexSalidaDetalle (IdKardexSalida, IdMaterial, Cantidad, Observacion)
        SELECT @NewId, IdMaterial, Cantidad, Observacion FROM @Items;

        -- Una salida cero sin entradas previas también conserva una fila de stock.
        INSERT INTO almacen.KardexStock (IdMaterial, TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento)
        SELECT n.IdMaterial, 0, 0, 0, @Fecha FROM @Nuevos n
        WHERE NOT EXISTS (SELECT 1 FROM almacen.KardexStock ks WHERE ks.IdMaterial = n.IdMaterial);

        UPDATE ks
        SET TotalSalida = ks.TotalSalida + n.Cantidad, Stock = ks.Stock - n.Cantidad,
            FechaUltimaMovimiento = @Fecha
        FROM almacen.KardexStock ks JOIN @Nuevos n ON n.IdMaterial = ks.IdMaterial;

        EXEC almacen.usp_StockProyecto_AplicarCambios @Cambios;

        COMMIT;
    SELECT ks.IdKardexSalida, m.IdEspecialidad, e.Nombre AS Especialidad,
        ks.IdProyecto, pr.NombreProyecto AS Proyecto, ks.NumeroDocumento,
        ks.Fecha, ks.Solicitante, ks.Observacion, d.IdKardexSalidaDetalle,
        d.IdMaterial, m.Codigo AS CodigoMaterial, m.Descripcion AS Nombre,
        d.Cantidad, d.Observacion AS DetalleObservacion, ks.FechaCreacion
    FROM almacen.KardexSalida ks
    JOIN almacen.KardexSalidaDetalle d ON d.IdKardexSalida = ks.IdKardexSalida
    JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
    JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
    LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = ks.IdProyecto
    WHERE ks.IdKardexSalida = @NewId
    ORDER BY d.IdKardexSalidaDetalle;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_KardexSalida_Actualizar
    @IdKardexSalida INT,
    @IdProyecto INT = NULL,
    @NumeroDocumento NVARCHAR(50) = NULL,
    @Fecha DATE,
    @Solicitante NVARCHAR(150),
    @Observacion NVARCHAR(250) = NULL,
    @Items almacen.TVP_KardexSalidaItem READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Serializar escrituras del Kardex manual, incluidas altas sin fila de stock.
        DECLARE @Bloqueo INT;
        EXEC @Bloqueo = sys.sp_getapplock
            @Resource = N'Inventario:Movimientos', @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 10000;
        IF @Bloqueo < 0
            THROW 51113, 'INVENTARIO_OCUPADO: No se pudo obtener el bloqueo de inventario. Reintente la operación.', 1;

        EXEC almacen.usp_StockProyecto_ExigirInicializacion;

        IF @Fecha IS NULL OR @Solicitante IS NULL OR LTRIM(RTRIM(@Solicitante)) = ''
            THROW 51100, 'CAMPO_REQUERIDO: fecha y solicitante son obligatorios.', 1;
        IF @IdProyecto IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idProyecto', 1;
        IF NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = @IdProyecto)
            THROW 51101, 'FK_NO_EXISTE: idProyecto', 1;
        IF NOT EXISTS (SELECT 1 FROM @Items)
            THROW 51100, 'CAMPO_REQUERIDO: items (debe tener al menos uno)', 1;
        IF EXISTS (SELECT 1 FROM @Items WHERE IdMaterial IS NULL OR Cantidad IS NULL OR Cantidad < 0)
            THROW 51103, 'CANTIDAD_INVALIDA: Los ítems requieren material y cantidad no negativa.', 1;
        IF EXISTS (
            SELECT 1 FROM @Items i
            LEFT JOIN maestra.Material m WITH (HOLDLOCK) ON m.IdMaterial = i.IdMaterial
            WHERE m.IdMaterial IS NULL
        )
            THROW 51101, 'FK_NO_EXISTE: items contiene un material inexistente.', 1;

        -- Agrupar antes de validar: un material puede repetirse en varios detalles.
        DECLARE @Nuevos TABLE (IdMaterial INT PRIMARY KEY, Cantidad DECIMAL(28,2));
        INSERT INTO @Nuevos SELECT IdMaterial, SUM(Cantidad) FROM @Items GROUP BY IdMaterial;

        IF NOT EXISTS (SELECT 1 FROM almacen.KardexSalida WHERE IdKardexSalida = @IdKardexSalida)
            THROW 51104, 'KARDEX_NO_ENCONTRADO: idKardexSalida', 1;

        DECLARE @ProyectoAnterior INT;
        SELECT @ProyectoAnterior = IdProyecto FROM almacen.KardexSalida WHERE IdKardexSalida = @IdKardexSalida;
        IF @ProyectoAnterior IS NULL
            THROW 51119, 'PROYECTO_MOVIMIENTO_REQUERIDO: Regularice el proyecto del movimiento histórico antes de modificarlo.', 1;

        DECLARE @Cambios almacen.TVP_StockProyectoCantidad;
        INSERT INTO @Cambios
        SELECT IdMaterial, IdProyecto, SUM(Cantidad)
        FROM (
            SELECT IdMaterial, @ProyectoAnterior AS IdProyecto, CAST(Cantidad AS DECIMAL(29,2)) AS Cantidad
            FROM almacen.KardexSalidaDetalle WHERE IdKardexSalida = @IdKardexSalida
            UNION ALL
            SELECT IdMaterial, @IdProyecto, -CAST(Cantidad AS DECIMAL(29,2)) FROM @Nuevos
        ) c
        GROUP BY IdMaterial, IdProyecto;

        DECLARE @Diff TABLE (IdMaterial INT PRIMARY KEY, Cantidad DECIMAL(29,2));
        ;WITH Anteriores AS (
            SELECT IdMaterial, CAST(SUM(Cantidad) AS DECIMAL(28,2)) AS Cantidad FROM almacen.KardexSalidaDetalle
            WHERE IdKardexSalida = @IdKardexSalida GROUP BY IdMaterial
        )
        INSERT INTO @Diff
        SELECT COALESCE(n.IdMaterial, a.IdMaterial), ISNULL(n.Cantidad, 0) - ISNULL(a.Cantidad, 0)
        FROM @Nuevos n FULL JOIN Anteriores a ON a.IdMaterial = n.IdMaterial;

        IF EXISTS (
            SELECT 1 FROM @Diff d LEFT JOIN almacen.KardexStock ks ON ks.IdMaterial = d.IdMaterial
            WHERE d.Cantidad > ISNULL(ks.Stock, 0)
        )
        BEGIN
            DECLARE @Detalle NVARCHAR(MAX);
            SELECT @Detalle = STRING_AGG(CONVERT(NVARCHAR(MAX), CONCAT(
                'idMaterial=', d.IdMaterial, ' disponible=', ISNULL(ks.Stock, 0), ' solicitado=', d.Cantidad)), N'; ')
            FROM @Diff d LEFT JOIN almacen.KardexStock ks ON ks.IdMaterial = d.IdMaterial
            WHERE d.Cantidad > ISNULL(ks.Stock, 0);
            DECLARE @Mensaje NVARCHAR(2048) = LEFT(CONCAT('STOCK_INSUFICIENTE: ', @Detalle), 2048);
            THROW 51110, @Mensaje, 1;
        END;

        EXEC almacen.usp_StockProyecto_ValidarCambios @Cambios;

        INSERT INTO almacen.KardexStock (IdMaterial, TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento)
        SELECT d.IdMaterial, 0, 0, 0, @Fecha FROM @Diff d
        WHERE NOT EXISTS (SELECT 1 FROM almacen.KardexStock ks WHERE ks.IdMaterial = d.IdMaterial);

        UPDATE ks
        SET TotalSalida = ks.TotalSalida + d.Cantidad, Stock = ks.Stock - d.Cantidad,
            FechaUltimaMovimiento = @Fecha
        FROM almacen.KardexStock ks JOIN @Diff d ON d.IdMaterial = ks.IdMaterial;

        UPDATE almacen.KardexSalida
        SET IdProyecto = @IdProyecto, NumeroDocumento = @NumeroDocumento, Fecha = @Fecha,
            Solicitante = @Solicitante, Observacion = @Observacion
        WHERE IdKardexSalida = @IdKardexSalida;
        DELETE FROM almacen.KardexSalidaDetalle WHERE IdKardexSalida = @IdKardexSalida;
        INSERT INTO almacen.KardexSalidaDetalle (IdKardexSalida, IdMaterial, Cantidad, Observacion)
        SELECT @IdKardexSalida, IdMaterial, Cantidad, Observacion FROM @Items;

        EXEC almacen.usp_StockProyecto_AplicarCambios @Cambios;

        COMMIT;
    SELECT ks.IdKardexSalida, m.IdEspecialidad, e.Nombre AS Especialidad,
        ks.IdProyecto, pr.NombreProyecto AS Proyecto, ks.NumeroDocumento,
        ks.Fecha, ks.Solicitante, ks.Observacion, d.IdKardexSalidaDetalle,
        d.IdMaterial, m.Codigo AS CodigoMaterial, m.Descripcion AS Nombre,
        d.Cantidad, d.Observacion AS DetalleObservacion, ks.FechaCreacion
    FROM almacen.KardexSalida ks
    JOIN almacen.KardexSalidaDetalle d ON d.IdKardexSalida = ks.IdKardexSalida
    JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
    JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
    LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = ks.IdProyecto
    WHERE ks.IdKardexSalida = @IdKardexSalida
    ORDER BY d.IdKardexSalidaDetalle;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_KardexSalida_Eliminar
    @IdKardexSalida INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- Serializar escrituras del Kardex manual, incluidas altas sin fila de stock.
        DECLARE @Bloqueo INT;
        EXEC @Bloqueo = sys.sp_getapplock
            @Resource = N'Inventario:Movimientos', @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 10000;
        IF @Bloqueo < 0
            THROW 51113, 'INVENTARIO_OCUPADO: No se pudo obtener el bloqueo de inventario. Reintente la operación.', 1;

        EXEC almacen.usp_StockProyecto_ExigirInicializacion;

        IF NOT EXISTS (SELECT 1 FROM almacen.KardexSalida WHERE IdKardexSalida = @IdKardexSalida)
            THROW 51104, 'KARDEX_NO_ENCONTRADO: idKardexSalida', 1;

        DECLARE @IdProyecto INT;
        SELECT @IdProyecto = IdProyecto FROM almacen.KardexSalida WHERE IdKardexSalida = @IdKardexSalida;
        IF @IdProyecto IS NULL
            THROW 51119, 'PROYECTO_MOVIMIENTO_REQUERIDO: Regularice el proyecto del movimiento histórico antes de eliminarlo.', 1;

        DECLARE @Cambios almacen.TVP_StockProyectoCantidad;
        INSERT INTO @Cambios
        SELECT IdMaterial, @IdProyecto, SUM(Cantidad)
        FROM almacen.KardexSalidaDetalle WHERE IdKardexSalida = @IdKardexSalida GROUP BY IdMaterial;

        ;WITH Reponer AS (
            SELECT IdMaterial, CAST(SUM(Cantidad) AS DECIMAL(28,2)) AS Cantidad FROM almacen.KardexSalidaDetalle
            WHERE IdKardexSalida = @IdKardexSalida GROUP BY IdMaterial
        )
        UPDATE ks
        SET TotalSalida = ks.TotalSalida - r.Cantidad, Stock = ks.Stock + r.Cantidad
        FROM almacen.KardexStock ks JOIN Reponer r ON r.IdMaterial = ks.IdMaterial;
        DELETE FROM almacen.KardexSalida WHERE IdKardexSalida = @IdKardexSalida;

        EXEC almacen.usp_StockProyecto_AplicarCambios @Cambios;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_Kardex_StockActual_Listar
    @IdEspecialidad INT = NULL,
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ks.IdKardexStock, ks.IdMaterial, m.Codigo AS CodigoMaterial,
        m.Descripcion AS Nombre, m.UnidadMedida, m.IdEspecialidad,
        e.Nombre AS Especialidad, ks.TotalEntrada, ks.TotalSalida,
        ks.Stock, ks.FechaUltimaMovimiento
    FROM almacen.KardexStock ks
    JOIN maestra.Material m ON m.IdMaterial = ks.IdMaterial
    JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
    WHERE (@IdEspecialidad IS NULL OR m.IdEspecialidad = @IdEspecialidad)
      AND (@FechaDesde IS NULL OR ks.FechaUltimaMovimiento >= @FechaDesde)
      AND (@FechaHasta IS NULL OR ks.FechaUltimaMovimiento <= @FechaHasta)
    ORDER BY e.Nombre, m.Descripcion;
END;
GO
