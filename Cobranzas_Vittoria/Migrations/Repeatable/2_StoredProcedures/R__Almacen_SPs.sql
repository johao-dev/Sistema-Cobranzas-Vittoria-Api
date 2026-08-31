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
-- Modulo Inventario (Kardex manual entradas/salidas/stock)
-- Version 1.2.0. Los siguientes SPs son 100% nuevos y no modifican
-- ningun objeto existente en este archivo. Rigen el modulo Kardex manual
-- (entradas, salidas y stock), independiente del flujo de Compras que
-- sigue usando almacen.KardexMovimiento + los SPs legacy de arriba.
--
-- Convenciones de errores:
--   - Rango SQL: 51100-51199 (reservado para este modulo).
--   - Formato del mensaje: 'CODIGO: detalle' (ej: 'STOCK_INSUFICIENTE:
--     idMaterial=7 disponible=3 solicitado=10'). El Application/Common/
--     SqlExceptionTranslator del backend parsea este prefijo para mapear
--     a un codigoError estructurado en la respuesta 400/422.
--   - Transacciones: SET XACT_ABORT ON + TRY/CATCH explicito + ROLLBACK
--     en CATCH. KardexStock se mantiene en la MISMA transaccion que
--     KardexEntrada/KardexSalida (insert/update/delete + diff de stock).
-- =============================================================================


-- -----------------------------------------------------------------------------
-- usp_KardexEntrada_Listar
--   Lista entradas manuales con filtros opcionales. Ordena por fecha DESC
--   para que el front vea primero lo mas reciente.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [almacen].[usp_KardexEntrada_Listar]
    @IdEspecialidad INT = NULL,
    @IdProyecto     INT = NULL,
    @IdProveedor    INT = NULL,
    @FechaDesde     DATE = NULL,
    @FechaHasta     DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ke.IdKardexEntrada,
        ke.IdEspecialidad,
        e.Nombre          AS Especialidad,
        ke.IdMaterial,
        m.Codigo          AS CodigoMaterial,
        m.Descripcion     AS Nombre,
        ke.IdProveedor,
        p.RazonSocial     AS Proveedor,
        ke.IdProyecto,
        pr.NombreProyecto AS Proyecto,
        ke.NumeroDocumento,
        ke.Fecha,
        ke.Cantidad,
        ke.Observacion,
        ke.FechaCreacion
    FROM almacen.KardexEntrada ke
    INNER JOIN maestra.Material      m  ON m.IdMaterial     = ke.IdMaterial
    INNER JOIN maestra.Especialidad  e  ON e.IdEspecialidad = ke.IdEspecialidad
    LEFT  JOIN maestra.Proveedor     p  ON p.IdProveedor    = ke.IdProveedor
    LEFT  JOIN maestra.Proyecto      pr ON pr.IdProyecto    = ke.IdProyecto
    WHERE (@IdEspecialidad IS NULL OR ke.IdEspecialidad = @IdEspecialidad)
      AND (@IdProyecto     IS NULL OR ke.IdProyecto     = @IdProyecto)
      AND (@IdProveedor    IS NULL OR ke.IdProveedor    = @IdProveedor)
      AND (@FechaDesde     IS NULL OR ke.Fecha         >= @FechaDesde)
      AND (@FechaHasta     IS NULL OR ke.Fecha         <= @FechaHasta)
    ORDER BY
        ke.Fecha DESC,
        ke.IdKardexEntrada DESC;
END;
GO


-- -----------------------------------------------------------------------------
-- usp_KardexEntrada_Registrar
--   Crea una entrada manual y actualiza KardexStock dentro de la misma TX.
--   Idempotente solo por la PK identity (no UPSERT).
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [almacen].[usp_KardexEntrada_Registrar]
    @IdEspecialidad  INT,
    @IdMaterial      INT,
    @IdProveedor     INT = NULL,
    @IdProyecto      INT = NULL,
    @NumeroDocumento NVARCHAR(50) = NULL,
    @Fecha           DATE,
    @Cantidad        DECIMAL(18, 2),
    @Observacion     NVARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- ---------- Validaciones ----------
        IF @IdEspecialidad IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idEspecialidad', 1;
        IF @IdMaterial IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idMaterial', 1;
        IF @Fecha IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: fecha', 1;
        IF @Cantidad IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: cantidad', 1;
        IF @Cantidad < 0
            THROW 51103, 'CANTIDAD_INVALIDA: la cantidad no puede ser negativa', 1;

        IF NOT EXISTS (SELECT 1 FROM maestra.Especialidad WHERE IdEspecialidad = @IdEspecialidad)
            THROW 51101, 'FK_NO_EXISTE: idEspecialidad', 1;
        IF NOT EXISTS (SELECT 1 FROM maestra.Material WHERE IdMaterial = @IdMaterial)
            THROW 51101, 'FK_NO_EXISTE: idMaterial', 1;
        IF @IdProveedor IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM maestra.Proveedor WHERE IdProveedor = @IdProveedor)
            THROW 51101, 'FK_NO_EXISTE: idProveedor', 1;
        IF @IdProyecto IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = @IdProyecto)
            THROW 51101, 'FK_NO_EXISTE: idProyecto', 1;

        -- ---------- Insert en KardexEntrada ----------
        DECLARE @NewId INT;
        INSERT INTO almacen.KardexEntrada (
            IdEspecialidad, IdMaterial, IdProveedor, IdProyecto,
            NumeroDocumento, Fecha, Cantidad, Observacion
        ) VALUES (
            @IdEspecialidad, @IdMaterial, @IdProveedor, @IdProyecto,
            @NumeroDocumento, @Fecha, @Cantidad, @Observacion
        );
        SET @NewId = SCOPE_IDENTITY();

        -- ---------- Upsert KardexStock (stock global por material + especialidad) ----------
        IF EXISTS (
            SELECT 1 FROM almacen.KardexStock
            WHERE IdMaterial = @IdMaterial
              AND IdEspecialidad = @IdEspecialidad
        )
        BEGIN
            UPDATE almacen.KardexStock
            SET TotalEntrada = TotalEntrada + @Cantidad,
                Stock        = Stock        + @Cantidad,
                FechaUltimaMovimiento = @Fecha
            WHERE IdMaterial = @IdMaterial
              AND IdEspecialidad = @IdEspecialidad;
        END
        ELSE
        BEGIN
            INSERT INTO almacen.KardexStock (
                IdMaterial, IdEspecialidad,
                TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento
            ) VALUES (
                @IdMaterial, @IdEspecialidad,
                @Cantidad, 0, @Cantidad, @Fecha
            );
        END

        COMMIT;

        -- Devolver la fila insertada con los joins que el front espera.
        SELECT
            ke.IdKardexEntrada,
            ke.IdEspecialidad,
            e.Nombre          AS Especialidad,
            ke.IdMaterial,
            m.Codigo          AS CodigoMaterial,
            m.Descripcion     AS Nombre,
            ke.IdProveedor,
            p.RazonSocial     AS Proveedor,
            ke.IdProyecto,
            pr.NombreProyecto AS Proyecto,
            ke.NumeroDocumento,
            ke.Fecha,
            ke.Cantidad,
            ke.Observacion,
            ke.FechaCreacion
        FROM almacen.KardexEntrada ke
        INNER JOIN maestra.Material      m  ON m.IdMaterial     = ke.IdMaterial
        INNER JOIN maestra.Especialidad  e  ON e.IdEspecialidad = ke.IdEspecialidad
        LEFT  JOIN maestra.Proveedor     p  ON p.IdProveedor    = ke.IdProveedor
        LEFT  JOIN maestra.Proyecto      pr ON pr.IdProyecto    = ke.IdProyecto
        WHERE ke.IdKardexEntrada = @NewId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- usp_KardexEntrada_Actualizar
--   Actualiza una entrada. Si cambia (IdMaterial, IdEspecialidad)
--   se hace rollback del stock en la dupla vieja y aplicacion en la nueva,
--   todo en la misma TX. Si la dupla no cambia, se aplica un diff simple.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [almacen].[usp_KardexEntrada_Actualizar]
    @IdKardexEntrada INT,
    @IdEspecialidad  INT,
    @IdMaterial      INT,
    @IdProveedor     INT = NULL,
    @IdProyecto      INT = NULL,
    @NumeroDocumento NVARCHAR(50) = NULL,
    @Fecha           DATE,
    @Cantidad        DECIMAL(18, 2),
    @Observacion     NVARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- ---------- Validaciones de entrada ----------
        IF @IdKardexEntrada IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idKardexEntrada', 1;
        IF @IdEspecialidad IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idEspecialidad', 1;
        IF @IdMaterial IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idMaterial', 1;
        IF @Fecha IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: fecha', 1;
        IF @Cantidad IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: cantidad', 1;
        IF @Cantidad < 0
            THROW 51103, 'CANTIDAD_INVALIDA: la cantidad no puede ser negativa', 1;

        IF NOT EXISTS (SELECT 1 FROM maestra.Especialidad WHERE IdEspecialidad = @IdEspecialidad)
            THROW 51101, 'FK_NO_EXISTE: idEspecialidad', 1;
        IF NOT EXISTS (SELECT 1 FROM maestra.Material WHERE IdMaterial = @IdMaterial)
            THROW 51101, 'FK_NO_EXISTE: idMaterial', 1;
        IF @IdProveedor IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM maestra.Proveedor WHERE IdProveedor = @IdProveedor)
            THROW 51101, 'FK_NO_EXISTE: idProveedor', 1;
        IF @IdProyecto IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = @IdProyecto)
            THROW 51101, 'FK_NO_EXISTE: idProyecto', 1;

        -- ---------- Leer valores viejos ----------
        DECLARE @OldIdMaterial INT, @OldIdEspecialidad INT, @OldIdProyecto INT,
                @OldCantidad DECIMAL(18,2);
        SELECT
            @OldIdMaterial     = IdMaterial,
            @OldIdEspecialidad = IdEspecialidad,
            @OldIdProyecto     = IdProyecto,
            @OldCantidad       = Cantidad
        FROM almacen.KardexEntrada
        WHERE IdKardexEntrada = @IdKardexEntrada;

        IF @OldIdMaterial IS NULL
            THROW 51104, 'KARDEX_NO_ENCONTRADO: idKardexEntrada', 1;

        -- ---------- Update de la entrada ----------
        UPDATE almacen.KardexEntrada
        SET IdEspecialidad  = @IdEspecialidad,
            IdMaterial      = @IdMaterial,
            IdProveedor     = @IdProveedor,
            IdProyecto      = @IdProyecto,
            NumeroDocumento = @NumeroDocumento,
            Fecha           = @Fecha,
            Cantidad        = @Cantidad,
            Observacion     = @Observacion
        WHERE IdKardexEntrada = @IdKardexEntrada;

        -- ---------- Ajustar KardexStock (stock global por material + especialidad) ----------
        DECLARE @Diff DECIMAL(18,2) = @Cantidad - @OldCantidad;

        IF (@OldIdMaterial = @IdMaterial)
           AND (@OldIdEspecialidad = @IdEspecialidad)
        BEGIN
            -- Misma dupla material+especialidad: diff simple.
            UPDATE almacen.KardexStock
            SET TotalEntrada = TotalEntrada + @Diff,
                Stock        = Stock        + @Diff,
                FechaUltimaMovimiento = @Fecha
            WHERE IdMaterial = @IdMaterial
              AND IdEspecialidad = @IdEspecialidad;
        END
        ELSE
        BEGIN
            -- Cambio de material o especialidad: rollback de la vieja + apply a la nueva.
            UPDATE almacen.KardexStock
            SET TotalEntrada = TotalEntrada - @OldCantidad,
                Stock        = Stock        - @OldCantidad
            WHERE IdMaterial = @OldIdMaterial
              AND IdEspecialidad = @OldIdEspecialidad;

            IF EXISTS (
                SELECT 1 FROM almacen.KardexStock
                WHERE IdMaterial = @IdMaterial
                  AND IdEspecialidad = @IdEspecialidad
            )
            BEGIN
                UPDATE almacen.KardexStock
                SET TotalEntrada = TotalEntrada + @Cantidad,
                    Stock        = Stock        + @Cantidad,
                    FechaUltimaMovimiento = @Fecha
                WHERE IdMaterial = @IdMaterial
                  AND IdEspecialidad = @IdEspecialidad;
            END
            ELSE
            BEGIN
                INSERT INTO almacen.KardexStock (
                    IdMaterial, IdEspecialidad,
                    TotalEntrada, TotalSalida, Stock, FechaUltimaMovimiento
                ) VALUES (
                    @IdMaterial, @IdEspecialidad,
                    @Cantidad, 0, @Cantidad, @Fecha
                );
            END
        END

        COMMIT;

        -- Devolver la fila actualizada.
        SELECT
            ke.IdKardexEntrada,
            ke.IdEspecialidad,
            e.Nombre          AS Especialidad,
            ke.IdMaterial,
            m.Codigo          AS CodigoMaterial,
            m.Descripcion     AS Nombre,
            ke.IdProveedor,
            p.RazonSocial     AS Proveedor,
            ke.IdProyecto,
            pr.NombreProyecto AS Proyecto,
            ke.NumeroDocumento,
            ke.Fecha,
            ke.Cantidad,
            ke.Observacion,
            ke.FechaCreacion
        FROM almacen.KardexEntrada ke
        INNER JOIN maestra.Material      m  ON m.IdMaterial     = ke.IdMaterial
        INNER JOIN maestra.Especialidad  e  ON e.IdEspecialidad = ke.IdEspecialidad
        LEFT  JOIN maestra.Proveedor     p  ON p.IdProveedor    = ke.IdProveedor
        LEFT  JOIN maestra.Proyecto      pr ON pr.IdProyecto    = ke.IdProyecto
        WHERE ke.IdKardexEntrada = @IdKardexEntrada;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- usp_KardexEntrada_Eliminar
--   Elimina una entrada y descuenta su cantidad de KardexStock. Si la fila
--   de KardexStock queda con TotalEntrada=0 y Stock=0, no se elimina la
--   fila (se conserva el historial). Si la resta dejaria Stock<0, lanza
--   51111 (hay salidas posteriores que dependen de esta entrada).
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [almacen].[usp_KardexEntrada_Eliminar]
    @IdKardexEntrada INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        IF @IdKardexEntrada IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idKardexEntrada', 1;

        DECLARE @IdMaterial INT, @IdEspecialidad INT, @Cantidad DECIMAL(18,2);
        SELECT
            @IdMaterial     = IdMaterial,
            @IdEspecialidad = IdEspecialidad,
            @Cantidad       = Cantidad
        FROM almacen.KardexEntrada
        WHERE IdKardexEntrada = @IdKardexEntrada;

        IF @IdMaterial IS NULL
            THROW 51104, 'KARDEX_NO_ENCONTRADO: idKardexEntrada', 1;

        -- Verificar que al restar la cantidad el stock no quede negativo.
        DECLARE @StockActual DECIMAL(18,2);
        SELECT @StockActual = Stock
        FROM almacen.KardexStock
        WHERE IdMaterial = @IdMaterial
          AND IdEspecialidad = @IdEspecialidad;

        IF @StockActual IS NOT NULL AND @StockActual - @Cantidad < 0
        BEGIN
            DECLARE @MsgInconsistente NVARCHAR(1000) =
                N'STOCK_INCONSISTENTE_AL_ELIMINAR: idKardexEntrada='
                + CAST(@IdKardexEntrada AS NVARCHAR(20))
                + N' stockActual=' + CAST(@StockActual AS NVARCHAR(20))
                + N' cantidad=' + CAST(@Cantidad AS NVARCHAR(20));
            THROW 51111, @MsgInconsistente, 1;
        END;

        DELETE FROM almacen.KardexEntrada WHERE IdKardexEntrada = @IdKardexEntrada;

        UPDATE almacen.KardexStock
        SET TotalEntrada = TotalEntrada - @Cantidad,
            Stock        = Stock        - @Cantidad
        WHERE IdMaterial = @IdMaterial
          AND IdEspecialidad = @IdEspecialidad;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- usp_KardexSalida_Listar
--   Lista salidas manuales con sus items. Una fila por cada item
--   (KardexSalidaDetalle), repitiendo los datos de cabecera.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [almacen].[usp_KardexSalida_Listar]
    @IdEspecialidad INT = NULL,
    @IdProyecto     INT = NULL,
    @FechaDesde     DATE = NULL,
    @FechaHasta     DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ks.IdKardexSalida,
        ks.IdEspecialidad,
        e.Nombre          AS Especialidad,
        ks.IdProyecto,
        pr.NombreProyecto AS Proyecto,
        ks.NumeroDocumento,
        ks.Fecha,
        ks.Solicitante,
        ks.Observacion,
        ksd.IdKardexSalidaDetalle,
        ksd.IdMaterial,
        m.Codigo          AS CodigoMaterial,
        m.Descripcion     AS Nombre,
        ksd.Cantidad,
        ksd.Observacion   AS DetalleObservacion,
        ks.FechaCreacion
    FROM almacen.KardexSalida ks
    INNER JOIN almacen.KardexSalidaDetalle ksd ON ksd.IdKardexSalida = ks.IdKardexSalida
    INNER JOIN maestra.Material      m  ON m.IdMaterial     = ksd.IdMaterial
    INNER JOIN maestra.Especialidad  e  ON e.IdEspecialidad = ks.IdEspecialidad
    LEFT  JOIN maestra.Proyecto      pr ON pr.IdProyecto    = ks.IdProyecto
    WHERE (@IdEspecialidad IS NULL OR ks.IdEspecialidad = @IdEspecialidad)
      AND (@IdProyecto     IS NULL OR ks.IdProyecto     = @IdProyecto)
      AND (@FechaDesde     IS NULL OR ks.Fecha         >= @FechaDesde)
      AND (@FechaHasta     IS NULL OR ks.Fecha         <= @FechaHasta)
    ORDER BY
        ks.Fecha DESC,
        ks.IdKardexSalida DESC,
        ksd.IdKardexSalidaDetalle ASC;
END;
GO


-- -----------------------------------------------------------------------------
-- usp_KardexSalida_Registrar
--   Crea una salida manual con 1..N items. Antes de aplicar, valida que
--   exista stock suficiente para cada item; si falta, lanza 51110.
--   KardexStock se actualiza en la misma TX.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [almacen].[usp_KardexSalida_Registrar]
    @IdEspecialidad  INT,
    @IdProyecto      INT = NULL,
    @NumeroDocumento NVARCHAR(50) = NULL,
    @Fecha           DATE,
    @Solicitante     NVARCHAR(150),
    @Observacion     NVARCHAR(250) = NULL,
    @Items           almacen.TVP_KardexSalidaItem READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- ---------- Validaciones de cabecera ----------
        IF @IdEspecialidad IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idEspecialidad', 1;
        IF @Fecha IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: fecha', 1;
        IF @Solicitante IS NULL OR LTRIM(RTRIM(@Solicitante)) = ''
            THROW 51100, 'CAMPO_REQUERIDO: solicitante', 1;
        IF NOT EXISTS (SELECT 1 FROM @Items)
            THROW 51100, 'CAMPO_REQUERIDO: items (debe tener al menos uno)', 1;

        IF NOT EXISTS (SELECT 1 FROM maestra.Especialidad WHERE IdEspecialidad = @IdEspecialidad)
            THROW 51101, 'FK_NO_EXISTE: idEspecialidad', 1;
        IF @IdProyecto IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = @IdProyecto)
            THROW 51101, 'FK_NO_EXISTE: idProyecto', 1;

        -- ---------- Validar items: idMaterial existe, cantidad valida ----------
        IF EXISTS (
            SELECT 1 FROM @Items i
            WHERE i.IdMaterial IS NULL
               OR i.Cantidad IS NULL
               OR i.Cantidad < 0
        )
            THROW 51103, 'CANTIDAD_INVALIDA: items contiene filas con idMaterial NULL o cantidad invalida', 1;

        IF EXISTS (
            SELECT i.IdMaterial FROM @Items i
            LEFT JOIN maestra.Material m ON m.IdMaterial = i.IdMaterial
            WHERE m.IdMaterial IS NULL
        )
            THROW 51101, 'FK_NO_EXISTE: items contiene idMaterial que no existe en maestra.Material', 1;

        -- ---------- Validar stock para cada item ----------
        -- El stock es GLOBAL por (IdMaterial, IdEspecialidad). El IdProyecto
        -- de la salida solo es una etiqueta informativa; no segmenta stock.
        DECLARE @Insuficientes TABLE (
            IdMaterial INT,
            Solicitado DECIMAL(18,2),
            Disponible DECIMAL(18,2)
        );

        INSERT INTO @Insuficientes (IdMaterial, Solicitado, Disponible)
        SELECT
            i.IdMaterial,
            i.Cantidad,
            ISNULL(ks.Stock, 0)
        FROM @Items i
        LEFT JOIN almacen.KardexStock ks
            ON  ks.IdMaterial     = i.IdMaterial
            AND ks.IdEspecialidad = @IdEspecialidad
        WHERE ISNULL(ks.Stock, 0) < i.Cantidad;

        IF EXISTS (SELECT 1 FROM @Insuficientes)
        BEGIN
            DECLARE @DetalleInsuficiente NVARCHAR(1000) = N'';
            SELECT @DetalleInsuficiente = @DetalleInsuficiente
                + N' idMaterial=' + CAST(IdMaterial AS NVARCHAR(20))
                + N' disponible=' + CAST(Disponible AS NVARCHAR(20))
                + N' solicitado=' + CAST(Solicitado AS NVARCHAR(20)) + N';'
            FROM @Insuficientes;
            DECLARE @MsgStockInsuficiente NVARCHAR(1500) =
                N'STOCK_INSUFICIENTE:' + @DetalleInsuficiente;
            THROW 51110, @MsgStockInsuficiente, 1;
        END

        -- ---------- Insert cabecera ----------
        DECLARE @NewId INT;
        INSERT INTO almacen.KardexSalida (
            IdEspecialidad, IdProyecto, NumeroDocumento,
            Fecha, Solicitante, Observacion
        ) VALUES (
            @IdEspecialidad, @IdProyecto, @NumeroDocumento,
            @Fecha, @Solicitante, @Observacion
        );
        SET @NewId = SCOPE_IDENTITY();

        -- ---------- Insert detalle (1..N) ----------
        INSERT INTO almacen.KardexSalidaDetalle (
            IdKardexSalida, IdMaterial, Cantidad, Observacion
        )
        SELECT @NewId, i.IdMaterial, i.Cantidad, i.Observacion
        FROM @Items i;

        -- ---------- Actualizar KardexStock (stock global por material + especialidad) ----------
        ;WITH StockUpdate AS (
            SELECT
                i.IdMaterial,
                @IdEspecialidad  AS IdEspecialidad,
                SUM(i.Cantidad)  AS TotalCantidad
            FROM @Items i
            GROUP BY i.IdMaterial
        )
        UPDATE ks
        SET TotalSalida = ks.TotalSalida + su.TotalCantidad,
            Stock        = ks.Stock       - su.TotalCantidad,
            FechaUltimaMovimiento = @Fecha
        FROM almacen.KardexStock ks
        INNER JOIN StockUpdate su
            ON  su.IdMaterial     = ks.IdMaterial
            AND su.IdEspecialidad = ks.IdEspecialidad;

        COMMIT;

        -- Devolver la salida registrada con sus items.
        SELECT
            ks.IdKardexSalida,
            ks.IdEspecialidad,
            e.Nombre          AS Especialidad,
            ks.IdProyecto,
            pr.NombreProyecto AS Proyecto,
            ks.NumeroDocumento,
            ks.Fecha,
            ks.Solicitante,
            ks.Observacion,
            ksd.IdKardexSalidaDetalle,
            ksd.IdMaterial,
            m.Codigo          AS CodigoMaterial,
            m.Descripcion     AS Nombre,
            ksd.Cantidad,
            ksd.Observacion   AS DetalleObservacion,
            ks.FechaCreacion
        FROM almacen.KardexSalida ks
        INNER JOIN almacen.KardexSalidaDetalle ksd ON ksd.IdKardexSalida = ks.IdKardexSalida
        INNER JOIN maestra.Material      m  ON m.IdMaterial     = ksd.IdMaterial
        INNER JOIN maestra.Especialidad  e  ON e.IdEspecialidad = ks.IdEspecialidad
        LEFT  JOIN maestra.Proyecto      pr ON pr.IdProyecto    = ks.IdProyecto
        WHERE ks.IdKardexSalida = @NewId
        ORDER BY ksd.IdKardexSalidaDetalle ASC;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- usp_KardexSalida_Actualizar
--   Reemplaza cabecera + items en TX. Calcula el diff por dupla
--   (IdMaterial, IdEspecialidad) y lo aplica a KardexStock. Antes valida
--   que con la nueva lista ningun item deje el stock por debajo de lo ya consumido.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [almacen].[usp_KardexSalida_Actualizar]
    @IdKardexSalida  INT,
    @IdEspecialidad  INT,
    @IdProyecto      INT = NULL,
    @NumeroDocumento NVARCHAR(50) = NULL,
    @Fecha           DATE,
    @Solicitante     NVARCHAR(150),
    @Observacion     NVARCHAR(250) = NULL,
    @Items           almacen.TVP_KardexSalidaItem READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        -- ---------- Validaciones de cabecera ----------
        IF @IdKardexSalida IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idKardexSalida', 1;
        IF @IdEspecialidad IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idEspecialidad', 1;
        IF @Fecha IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: fecha', 1;
        IF @Solicitante IS NULL OR LTRIM(RTRIM(@Solicitante)) = ''
            THROW 51100, 'CAMPO_REQUERIDO: solicitante', 1;
        IF NOT EXISTS (SELECT 1 FROM @Items)
            THROW 51100, 'CAMPO_REQUERIDO: items (debe tener al menos uno)', 1;

        IF NOT EXISTS (SELECT 1 FROM almacen.KardexSalida WHERE IdKardexSalida = @IdKardexSalida)
            THROW 51104, 'KARDEX_NO_ENCONTRADO: idKardexSalida', 1;

        IF NOT EXISTS (SELECT 1 FROM maestra.Especialidad WHERE IdEspecialidad = @IdEspecialidad)
            THROW 51101, 'FK_NO_EXISTE: idEspecialidad', 1;
        IF @IdProyecto IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = @IdProyecto)
            THROW 51101, 'FK_NO_EXISTE: idProyecto', 1;

        -- ---------- Validar items ----------
        IF EXISTS (
            SELECT 1 FROM @Items i
            WHERE i.IdMaterial IS NULL OR i.Cantidad IS NULL OR i.Cantidad < 0
        )
            THROW 51103, 'CANTIDAD_INVALIDA: items contiene filas con idMaterial NULL o cantidad invalida', 1;

        IF EXISTS (
            SELECT i.IdMaterial FROM @Items i
            LEFT JOIN maestra.Material m ON m.IdMaterial = i.IdMaterial
            WHERE m.IdMaterial IS NULL
        )
            THROW 51101, 'FK_NO_EXISTE: items contiene idMaterial que no existe en maestra.Material', 1;

        -- ---------- Calcular diff por dupla (IdMaterial, IdEspecialidad) ----------
        -- diff > 0: la nueva salida pide MAS que la vieja -> hay que validar stock.
        -- diff < 0: la nueva salida pide MENOS -> el stock se repone automaticamente.
        -- Usamos una tabla temporal porque necesitamos reutilizar el diff en dos
        -- sentencias separadas (IF EXISTS y luego SELECT para armar el mensaje).
        DECLARE @Diff TABLE (
            IdMaterial INT          NOT NULL,
            Cantidad   DECIMAL(18,2) NOT NULL
        );

        ;WITH OldItems AS (
            SELECT ksd.IdMaterial, SUM(ksd.Cantidad) AS Cantidad
            FROM almacen.KardexSalidaDetalle ksd
            WHERE ksd.IdKardexSalida = @IdKardexSalida
            GROUP BY ksd.IdMaterial
        ),
        NewItems AS (
            SELECT i.IdMaterial, SUM(i.Cantidad) AS Cantidad
            FROM @Items i
            GROUP BY i.IdMaterial
        )
        INSERT INTO @Diff (IdMaterial, Cantidad)
        SELECT
            COALESCE(o.IdMaterial, n.IdMaterial)         AS IdMaterial,
            ISNULL(n.Cantidad, 0) - ISNULL(o.Cantidad, 0) AS Cantidad
        FROM NewItems n
        FULL OUTER JOIN OldItems o ON o.IdMaterial = n.IdMaterial;

        -- Validar que para los diffs positivos haya stock suficiente (stock global).
        IF EXISTS (
            SELECT 1
            FROM @Diff d
            LEFT JOIN almacen.KardexStock ks
                ON  ks.IdMaterial     = d.IdMaterial
                AND ks.IdEspecialidad = @IdEspecialidad
            WHERE d.Cantidad > 0
              AND ISNULL(ks.Stock, 0) < d.Cantidad
        )
        BEGIN
            DECLARE @DetalleInsuficienteUpd NVARCHAR(1000) = N'';
            SELECT @DetalleInsuficienteUpd = @DetalleInsuficienteUpd
                + N' idMaterial=' + CAST(d.IdMaterial AS NVARCHAR(20))
                + N' solicitado=' + CAST(d.Cantidad AS NVARCHAR(20))
                + N' disponible=' + CAST(ISNULL(ks.Stock, 0) AS NVARCHAR(20)) + N';'
            FROM @Diff d
            LEFT JOIN almacen.KardexStock ks
                ON  ks.IdMaterial     = d.IdMaterial
                AND ks.IdEspecialidad = @IdEspecialidad
            WHERE d.Cantidad > 0
              AND ISNULL(ks.Stock, 0) < d.Cantidad;
            DECLARE @MsgStockInsuficienteUpd NVARCHAR(1500) =
                N'STOCK_INSUFICIENTE:' + @DetalleInsuficienteUpd;
            THROW 51110, @MsgStockInsuficienteUpd, 1;
        END

        -- ---------- Update cabecera ----------
        UPDATE almacen.KardexSalida
        SET IdEspecialidad  = @IdEspecialidad,
            IdProyecto      = @IdProyecto,
            NumeroDocumento = @NumeroDocumento,
            Fecha           = @Fecha,
            Solicitante     = @Solicitante,
            Observacion     = @Observacion
        WHERE IdKardexSalida = @IdKardexSalida;

        -- ---------- Reemplazar detalle: borrar todo e insertar el nuevo ----------
        DELETE FROM almacen.KardexSalidaDetalle WHERE IdKardexSalida = @IdKardexSalida;

        INSERT INTO almacen.KardexSalidaDetalle (
            IdKardexSalida, IdMaterial, Cantidad, Observacion
        )
        SELECT @IdKardexSalida, i.IdMaterial, i.Cantidad, i.Observacion
        FROM @Items i;

        -- ---------- Aplicar diff a KardexStock ----------
        -- Reutilizamos @Diff (calculado arriba ANTES de borrar el detalle
        -- antiguo). Si recalcularamos aqui leyendo de KardexSalidaDetalle,
        -- OldItems seria 0 (el detalle ya fue reemplazado) y la formula
        -- Diff = New - Old daria siempre el valor nuevo completo,
        -- descontando o sumando de mas.
        -- Recordar: Cantidad del diff es New - Old. Positivo = la nueva
        -- salida pide MAS que la vieja (hay que descontar del stock);
        -- negativo = pide MENOS (se repone stock). Stock - CantidadDiff
        -- cubre ambos signos.
        UPDATE ks
        SET TotalSalida = ks.TotalSalida + d.Cantidad,
            Stock        = ks.Stock       - d.Cantidad,
            FechaUltimaMovimiento = @Fecha
        FROM almacen.KardexStock ks
        INNER JOIN @Diff d
            ON  d.IdMaterial     = ks.IdMaterial
            AND ks.IdEspecialidad = @IdEspecialidad
        WHERE d.Cantidad <> 0;

        COMMIT;

        -- Devolver la salida actualizada con sus items.
        SELECT
            ks.IdKardexSalida,
            ks.IdEspecialidad,
            e.Nombre          AS Especialidad,
            ks.IdProyecto,
            pr.NombreProyecto AS Proyecto,
            ks.NumeroDocumento,
            ks.Fecha,
            ks.Solicitante,
            ks.Observacion,
            ksd.IdKardexSalidaDetalle,
            ksd.IdMaterial,
            m.Codigo          AS CodigoMaterial,
            m.Descripcion     AS Nombre,
            ksd.Cantidad,
            ksd.Observacion   AS DetalleObservacion,
            ks.FechaCreacion
        FROM almacen.KardexSalida ks
        INNER JOIN almacen.KardexSalidaDetalle ksd ON ksd.IdKardexSalida = ks.IdKardexSalida
        INNER JOIN maestra.Material      m  ON m.IdMaterial     = ksd.IdMaterial
        INNER JOIN maestra.Especialidad  e  ON e.IdEspecialidad = ks.IdEspecialidad
        LEFT  JOIN maestra.Proyecto      pr ON pr.IdProyecto    = ks.IdProyecto
        WHERE ks.IdKardexSalida = @IdKardexSalida
        ORDER BY ksd.IdKardexSalidaDetalle ASC;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- usp_KardexSalida_Eliminar
--   Elimina una salida (CASCADE borra los detalles) y repone el stock.
--   Siempre es seguro: eliminar una salida solo aumenta el stock.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [almacen].[usp_KardexSalida_Eliminar]
    @IdKardexSalida INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        IF @IdKardexSalida IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: idKardexSalida', 1;

        DECLARE @IdEspecialidad INT, @IdProyecto INT;
        SELECT @IdEspecialidad = IdEspecialidad,
               @IdProyecto     = IdProyecto
        FROM almacen.KardexSalida
        WHERE IdKardexSalida = @IdKardexSalida;

        IF @IdEspecialidad IS NULL
            THROW 51104, 'KARDEX_NO_ENCONTRADO: idKardexSalida', 1;

        -- Calcular el total a reponer en el stock global.
        ;WITH Reponer AS (
            SELECT ksd.IdMaterial, SUM(ksd.Cantidad) AS Cantidad
            FROM almacen.KardexSalidaDetalle ksd
            WHERE ksd.IdKardexSalida = @IdKardexSalida
            GROUP BY ksd.IdMaterial
        )
        UPDATE ks
        SET TotalSalida = ks.TotalSalida - r.Cantidad,
            Stock        = ks.Stock       + r.Cantidad
        FROM almacen.KardexStock ks
        INNER JOIN Reponer r ON r.IdMaterial = ks.IdMaterial
        WHERE ks.IdEspecialidad = @IdEspecialidad;

        -- CASCADE borra los detalles.
        DELETE FROM almacen.KardexSalida WHERE IdKardexSalida = @IdKardexSalida;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO


-- -----------------------------------------------------------------------------
-- usp_Kardex_StockActual_Listar
--   Lee el inventario consolidado (almacen.KardexStock) con los joins
--   a maestra para que el front reciba CodigoMaterial, Nombre, UnidadMedida
--   y los nombres legibles. El stock es global por (IdMaterial, IdEspecialidad);
--   el parametro @IdProyecto se mantiene por compatibilidad del API pero se
--   ignora en el filtrado.
--
--   Filtros (todos opcionales):
--     @IdEspecialidad : por especialidad del kardex
--     @IdProyecto     : ignorado (compatibilidad de API)
--     @FechaDesde     : FechaUltimaMovimiento >= @FechaDesde
--     @FechaHasta     : FechaUltimaMovimiento <= @FechaHasta
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [almacen].[usp_Kardex_StockActual_Listar]
    @IdEspecialidad INT  = NULL,
    @IdProyecto     INT  = NULL,  -- mantenido por compatibilidad; no filtra
    @FechaDesde     DATE = NULL,
    @FechaHasta     DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ks.IdKardexStock,
        ks.IdMaterial,
        m.Codigo          AS CodigoMaterial,
        m.Descripcion     AS Nombre,
        m.UnidadMedida,
        ks.IdEspecialidad,
        e.Nombre          AS Especialidad,
        NULL              AS IdProyecto,
        NULL              AS Proyecto,
        ks.TotalEntrada,
        ks.TotalSalida,
        ks.Stock,
        ks.FechaUltimaMovimiento
    FROM almacen.KardexStock ks
    INNER JOIN maestra.Material      m  ON m.IdMaterial     = ks.IdMaterial
    INNER JOIN maestra.Especialidad  e  ON e.IdEspecialidad = ks.IdEspecialidad
    WHERE (@IdEspecialidad IS NULL OR ks.IdEspecialidad = @IdEspecialidad)
      AND (@FechaDesde IS NULL OR ks.FechaUltimaMovimiento >= @FechaDesde)
      AND (@FechaHasta IS NULL OR ks.FechaUltimaMovimiento <= @FechaHasta)
    ORDER BY
        e.Nombre,
        m.Descripcion;
END;
GO
