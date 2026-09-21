CREATE OR ALTER PROCEDURE [compras].[usp_Compra_Aceptar]
    @IdCompra INT,
    @IdUsuario INT = NULL,
    @Observacion NVARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @IdOrdenCompra INT, @FechaCompra DATE, @EstadoCompra VARCHAR(20),
            @EstadoOc NVARCHAR(30), @IdMoneda INT, @IdProyecto INT;

        SELECT @IdOrdenCompra = c.IdOrdenCompra, @FechaCompra = c.FechaCompra,
            @EstadoCompra = c.Estado
        FROM compras.Compra c WITH (UPDLOCK, HOLDLOCK)
        WHERE c.IdCompra = @IdCompra;

        IF @IdOrdenCompra IS NULL THROW 50065, 'Compra no existe.', 1;
        IF @EstadoCompra = 'ACEPTADA'
        BEGIN
            COMMIT TRANSACTION;
            SELECT 1 AS Ok, N'La compra ya estaba aceptada.' AS Mensaje;
            RETURN;
        END;
        IF @EstadoCompra <> 'REGISTRADA'
            THROW 51430, 'ESTADO_COMPRA_INVALIDO: solo una Compra REGISTRADA puede aceptarse.', 1;

        SELECT @EstadoOc = oc.Estado, @IdMoneda = oc.IdMoneda, @IdProyecto = r.IdProyecto
        FROM compras.OrdenCompra oc WITH (UPDLOCK, HOLDLOCK)
        JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
        WHERE oc.IdOrdenCompra = @IdOrdenCompra;

        IF @EstadoOc <> 'APROBADA'
            THROW 51431, 'ESTADO_OC_INVALIDO: la Compra solo puede aceptarse contra una OC APROBADA.', 1;
        IF EXISTS
        (
            SELECT IdMaterial, Cantidad FROM compras.OrdenCompraDetalle WHERE IdOrdenCompra = @IdOrdenCompra
            EXCEPT
            SELECT IdMaterial, Cantidad FROM compras.CompraDetalle WHERE IdCompra = @IdCompra
        ) OR EXISTS
        (
            SELECT IdMaterial, Cantidad FROM compras.CompraDetalle WHERE IdCompra = @IdCompra
            EXCEPT
            SELECT IdMaterial, Cantidad FROM compras.OrdenCompraDetalle WHERE IdOrdenCompra = @IdOrdenCompra
        )
            THROW 51432, 'COMPRA_NO_CUBRE_OC: materiales y cantidades deben coincidir completamente con la OC.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM compras.OrdenCompraDetalle od
            LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento =
                (SELECT IdRequerimiento FROM compras.OrdenCompra WHERE IdOrdenCompra = @IdOrdenCompra)
            LEFT JOIN compras.RequerimientoDetalle rd ON rd.IdRequerimiento = r.IdRequerimiento
                AND rd.IdMaterial = od.IdMaterial
            WHERE od.IdOrdenCompra = @IdOrdenCompra
              AND (rd.IdRequerimientoDetalle IS NULL OR rd.IdPresupuestoDetalle IS NULL)
        )
            THROW 51433, 'PARTIDA_REQUERIDA: todas las líneas de la OC deben tener partida presupuestaria.', 1;

        DECLARE @Movimientos compras.TVP_MovimientoEconomico;
        INSERT INTO @Movimientos
            (IdPresupuestoDetalle, TipoMovimiento, ClaveEvento, Origen, IdOrigen, Monto, Fecha, Observacion)
        SELECT rd.IdPresupuestoDetalle, 'LIBERACION',
            CONCAT('COMPRA:', @IdCompra, ':MATERIAL:', cd.IdMaterial, ':LIBERACION'),
            'COMPRA', @IdCompra, od.Subtotal, CONVERT(DATETIME2(0), @FechaCompra),
            N'Liberación del compromiso al aceptar la Compra'
        FROM compras.CompraDetalle cd
        JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = @IdOrdenCompra
        JOIN compras.OrdenCompraDetalle od ON od.IdOrdenCompra = oc.IdOrdenCompra
            AND od.IdMaterial = cd.IdMaterial
        JOIN compras.RequerimientoDetalle rd ON rd.IdRequerimiento = oc.IdRequerimiento
            AND rd.IdMaterial = cd.IdMaterial
        WHERE cd.IdCompra = @IdCompra
        UNION ALL
        SELECT rd.IdPresupuestoDetalle, 'EJECUCION',
            CONCAT('COMPRA:', @IdCompra, ':MATERIAL:', cd.IdMaterial, ':EJECUCION'),
            'COMPRA', @IdCompra, cd.Subtotal, CONVERT(DATETIME2(0), @FechaCompra),
            N'Ejecución presupuestaria por Compra aceptada'
        FROM compras.CompraDetalle cd
        JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = @IdOrdenCompra
        JOIN compras.RequerimientoDetalle rd ON rd.IdRequerimiento = oc.IdRequerimiento
            AND rd.IdMaterial = cd.IdMaterial
        WHERE cd.IdCompra = @IdCompra;

        EXEC compras.usp_IntegracionEconomica_RegistrarLote
            @IdMoneda = @IdMoneda, @IdProyecto = @IdProyecto, @Movimientos = @Movimientos;

        UPDATE compras.Compra SET Aceptada = 1, Estado = 'ACEPTADA' WHERE IdCompra = @IdCompra;

        INSERT INTO almacen.KardexMovimiento
            (IdMaterial, IdEspecialidad, TipoMovimiento, FechaMovimiento,
             CantidadEntrada, CantidadSalida, StockResultante, IdCompra, IdOrdenCompra,
             Observacion, FechaIngresoAlmacen, FechaSalidaAlmacen, FechaCreacion)
        SELECT cd.IdMaterial, m.IdEspecialidad, N'ENTRADA', @FechaCompra,
            cd.Cantidad, 0,
            ISNULL((SELECT TOP 1 km.StockResultante FROM almacen.KardexMovimiento km WITH (UPDLOCK, HOLDLOCK)
                WHERE km.IdMaterial = cd.IdMaterial
                ORDER BY km.FechaMovimiento DESC, km.IdKardexMovimiento DESC), 0) + cd.Cantidad,
            @IdCompra, @IdOrdenCompra, N'Ingreso por compra aceptada', @FechaCompra, NULL, SYSDATETIME()
        FROM compras.CompraDetalle cd
        JOIN maestra.Material m ON m.IdMaterial = cd.IdMaterial
        WHERE cd.IdCompra = @IdCompra
          AND NOT EXISTS (SELECT 1 FROM almacen.KardexMovimiento km
              WHERE km.IdCompra = @IdCompra AND km.IdMaterial = cd.IdMaterial);

        UPDATE compras.OrdenCompra SET Estado = 'ATENDIDA' WHERE IdOrdenCompra = @IdOrdenCompra;
        INSERT INTO compras.OrdenCompraHistorial
            (IdOrdenCompra, EstadoAnterior, EstadoNuevo, IdUsuario, Observacion)
        VALUES (@IdOrdenCompra, 'APROBADA', 'ATENDIDA', @IdUsuario,
            COALESCE(@Observacion, N'OC atendida por aceptación definitiva de la Compra'));

        COMMIT TRANSACTION;
        SELECT 1 AS Ok, N'Compra aceptada; kardex y presupuesto actualizados.' AS Mensaje;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
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
        c.Estado,
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
        c.Estado,
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
        THROW 50060, 'NumeroCompra es requerido.', 1;
    IF NOT EXISTS (SELECT 1 FROM @Items)
        THROW 50064, 'Debe registrar items en la compra.', 1;
    IF EXISTS (SELECT 1 FROM @Items WHERE Cantidad <= 0 OR PrecioUnitario <= 0)
        THROW 51434, 'Los importes y cantidades de la Compra deben ser positivos.', 1;
    IF EXISTS (SELECT IdMaterial FROM @Items GROUP BY IdMaterial HAVING COUNT(*) > 1)
        THROW 51073, 'No se permiten materiales repetidos en una Compra.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        IF EXISTS (SELECT 1 FROM compras.Compra WITH (UPDLOCK, HOLDLOCK) WHERE NumeroCompra = @NumeroCompra)
            THROW 50061, 'Ya existe el Número de Compra.', 1;
        DECLARE @EstadoOc NVARCHAR(30);
        SELECT @EstadoOc = Estado FROM compras.OrdenCompra WITH (UPDLOCK, HOLDLOCK)
        WHERE IdOrdenCompra = @IdOrdenCompra;
        IF @EstadoOc IS NULL THROW 50062, 'Orden de compra no existe.', 1;
        IF @EstadoOc <> 'APROBADA'
            THROW 51431, 'ESTADO_OC_INVALIDO: solo una OC APROBADA admite registrar Compra.', 1;
        IF EXISTS (SELECT 1 FROM compras.Compra WITH (UPDLOCK, HOLDLOCK) WHERE IdOrdenCompra = @IdOrdenCompra)
            THROW 51435, 'COMPRA_UNICA_POR_OC: la OrdenCompra ya tiene una Compra.', 1;
        IF EXISTS
        (
            SELECT IdMaterial, Cantidad FROM compras.OrdenCompraDetalle WHERE IdOrdenCompra = @IdOrdenCompra
            EXCEPT SELECT IdMaterial, Cantidad FROM @Items
        ) OR EXISTS
        (
            SELECT IdMaterial, Cantidad FROM @Items
            EXCEPT SELECT IdMaterial, Cantidad FROM compras.OrdenCompraDetalle WHERE IdOrdenCompra = @IdOrdenCompra
        )
            THROW 51432, 'COMPRA_NO_CUBRE_OC: materiales y cantidades deben coincidir completamente con la OC.', 1;

        DECLARE @MontoTotal DECIMAL(18,2), @SubtotalSinIGV DECIMAL(18,2), @MontoIGV DECIMAL(18,2);
        SELECT @MontoTotal = ROUND(SUM(Cantidad * PrecioUnitario), 2) FROM @Items;
        SET @SubtotalSinIGV = CASE WHEN ISNULL(@IncluyeIGV, 0) = 1
            THEN ROUND(@MontoTotal / 1.18, 2) ELSE @MontoTotal END;
        SET @MontoIGV = CASE WHEN ISNULL(@IncluyeIGV, 0) = 1
            THEN ROUND(@MontoTotal - @SubtotalSinIGV, 2) ELSE 0 END;

        INSERT INTO compras.Compra
            (NumeroCompra, IdOrdenCompra, FechaCompra, Aceptada, Estado, IncluyeIGV,
             SubtotalSinIGV, MontoIGV, MontoTotal, Observacion, FechaCreacion)
        VALUES (@NumeroCompra, @IdOrdenCompra, @FechaCompra, 0, 'REGISTRADA', @IncluyeIGV,
            @SubtotalSinIGV, @MontoIGV, @MontoTotal, @Observacion, SYSDATETIME());
        DECLARE @IdCompra INT = CONVERT(INT, SCOPE_IDENTITY());

        INSERT INTO compras.CompraDetalle (IdCompra, IdMaterial, Cantidad, PrecioUnitario)
        SELECT @IdCompra, IdMaterial, Cantidad, PrecioUnitario FROM @Items;

        INSERT INTO compras.CompraDocumento
            (IdCompra, TipoDocumento, NumeroDocumento, RutaArchivo, FechaDocumento,
             Monto, Observacion, NombreArchivo, Extension, FechaCreacion)
        SELECT @IdCompra, TipoDocumento, NumeroDocumento, RutaArchivo, FechaDocumento,
            Monto, Observacion,
            RIGHT(RutaArchivo, CHARINDEX('/', REVERSE(RutaArchivo + '/')) - 1),
            CASE WHEN CHARINDEX('.', RutaArchivo) > 0
                THEN RIGHT(RutaArchivo, CHARINDEX('.', REVERSE(RutaArchivo)) - 1) END,
            SYSDATETIME()
        FROM @Documentos;

        COMMIT TRANSACTION;
        SELECT @IdCompra AS IdCompra, @SubtotalSinIGV AS SubtotalSinIGV,
            @MontoIGV AS MontoIGV, @MontoTotal AS MontoTotal, @IncluyeIGV AS IncluyeIGV;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
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
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM @Items) THROW 51444, 'La orden debe tener items.', 1;
    IF EXISTS (SELECT 1 FROM @Items WHERE IdProveedor <= 0 OR Cantidad <= 0 OR PrecioUnitario <= 0)
        THROW 51442, 'Proveedor, cantidades y precios de la OC deben ser positivos.', 1;
    IF EXISTS (SELECT IdMaterial FROM @Items GROUP BY IdMaterial HAVING COUNT(*) > 1)
        THROW 51071, 'No se permiten materiales repetidos en una OC.', 1;
    IF NOT EXISTS (SELECT 1 FROM maestra.Moneda WHERE IdMoneda = @IdMoneda AND Activo = 1)
        THROW 51070, 'La moneda no existe o está inactiva.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @Estado NVARCHAR(30), @IdRequerimientoActual INT, @IdMonedaActual INT,
            @VersionActual INT, @IdProyecto INT;
        SELECT @Estado = oc.Estado, @IdRequerimientoActual = oc.IdRequerimiento,
            @IdMonedaActual = oc.IdMoneda, @VersionActual = oc.VersionEconomica,
            @IdProyecto = r.IdProyecto
        FROM compras.OrdenCompra oc WITH (UPDLOCK, HOLDLOCK)
        JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
        WHERE oc.IdOrdenCompra = @IdOrdenCompra;
        IF @Estado IS NULL THROW 50057, 'Orden de compra no existe.', 1;
        IF @Estado NOT IN ('REGISTRADA', 'APROBADA')
            THROW 51445, 'OC_NO_EDITABLE: solo REGISTRADA o APROBADA puede modificarse.', 1;
        IF @Estado = 'APROBADA' AND (@IdRequerimiento <> @IdRequerimientoActual OR @IdMoneda <> @IdMonedaActual)
            THROW 51446, 'OC_APROBADA_IDENTIDAD_INMUTABLE: no puede cambiar Requerimiento ni Moneda.', 1;
        IF @Estado = 'APROBADA' AND EXISTS
        (
            SELECT 1
            FROM compras.Compra c WITH (UPDLOCK, HOLDLOCK)
            WHERE c.IdOrdenCompra = @IdOrdenCompra
              AND
              (
                  c.Estado = 'ACEPTADA'
                  OR EXISTS
                  (
                      SELECT IdMaterial, Cantidad FROM compras.CompraDetalle WHERE IdCompra = c.IdCompra
                      EXCEPT SELECT IdMaterial, Cantidad FROM @Items
                  )
                  OR EXISTS
                  (
                      SELECT IdMaterial, Cantidad FROM @Items
                      EXCEPT SELECT IdMaterial, Cantidad FROM compras.CompraDetalle WHERE IdCompra = c.IdCompra
                  )
              )
        )
            THROW 51447, 'OC_CON_COMPRA_INCOMPATIBLE: la modificación no puede invalidar una Compra registrada o aceptada.', 1;
        IF EXISTS
        (
            SELECT IdMaterial FROM @Items
            EXCEPT SELECT IdMaterial FROM compras.RequerimientoDetalle
                WHERE IdRequerimiento = @IdRequerimiento
        )
            THROW 51443, 'MATERIAL_FUERA_REQUERIMIENTO: toda línea de OC debe provenir del Requerimiento.', 1;

        IF @Estado = 'APROBADA'
        BEGIN
            IF EXISTS
            (
                SELECT 1 FROM @Items i
                LEFT JOIN compras.RequerimientoDetalle rd
                    ON rd.IdRequerimiento = @IdRequerimiento AND rd.IdMaterial = i.IdMaterial
                WHERE rd.IdPresupuestoDetalle IS NULL
            )
                THROW 51433, 'PARTIDA_REQUERIDA: todas las líneas deben tener partida presupuestaria.', 1;

            DECLARE @NuevaVersion INT = @VersionActual + 1;
            DECLARE @Ajustes compras.TVP_MovimientoEconomico;
            ;WITH Materiales AS
            (
                SELECT IdMaterial FROM compras.OrdenCompraDetalle WHERE IdOrdenCompra = @IdOrdenCompra
                UNION SELECT IdMaterial FROM @Items
            ), Importes AS
            (
                SELECT m.IdMaterial, rd.IdPresupuestoDetalle,
                    COALESCE(ant.Subtotal, 0) AS ImporteAnterior,
                    COALESCE(CONVERT(DECIMAL(18,2), ROUND(nuevo.Cantidad * nuevo.PrecioUnitario, 2)), 0) AS ImporteNuevo
                FROM Materiales m
                LEFT JOIN compras.OrdenCompraDetalle ant ON ant.IdOrdenCompra = @IdOrdenCompra
                    AND ant.IdMaterial = m.IdMaterial
                LEFT JOIN @Items nuevo ON nuevo.IdMaterial = m.IdMaterial
                JOIN compras.RequerimientoDetalle rd ON rd.IdRequerimiento = @IdRequerimiento
                    AND rd.IdMaterial = m.IdMaterial
            )
            INSERT INTO @Ajustes
                (IdPresupuestoDetalle, TipoMovimiento, ClaveEvento, Origen, IdOrigen, Monto, Fecha, Observacion)
            SELECT IdPresupuestoDetalle,
                CASE WHEN ImporteNuevo > ImporteAnterior THEN 'COMPROMISO' ELSE 'LIBERACION' END,
                CONCAT('OC:', @IdOrdenCompra, ':MOD:', @NuevaVersion, ':MATERIAL:', IdMaterial, ':',
                    CASE WHEN ImporteNuevo > ImporteAnterior THEN 'COMPROMISO' ELSE 'LIBERACION' END),
                'ORDEN_COMPRA', @IdOrdenCompra, ABS(ImporteNuevo - ImporteAnterior), SYSDATETIME(),
                N'Ajuste económico por modificación de OrdenCompra aprobada'
            FROM Importes WHERE ImporteNuevo <> ImporteAnterior;

            EXEC compras.usp_IntegracionEconomica_RegistrarLote
                @IdMoneda = @IdMonedaActual, @IdProyecto = @IdProyecto, @Movimientos = @Ajustes;
            UPDATE compras.OrdenCompra SET VersionEconomica = @NuevaVersion
            WHERE IdOrdenCompra = @IdOrdenCompra;
        END;

        UPDATE compras.OrdenCompra
        SET NumeroOrdenCompra = @NumeroOrdenCompra, IdRequerimiento = @IdRequerimiento,
            IdMoneda = @IdMoneda, FechaOrdenCompra = @FechaOrdenCompra,
            Descripcion = @Descripcion, RutaPdf = @RutaPdf
        WHERE IdOrdenCompra = @IdOrdenCompra;

        DELETE FROM compras.OrdenCompraDetalle WHERE IdOrdenCompra = @IdOrdenCompra;
        INSERT INTO compras.OrdenCompraDetalle
            (IdOrdenCompra, IdMaterial, Cantidad, IdProveedor, PrecioUnitario)
        SELECT @IdOrdenCompra, IdMaterial, Cantidad, IdProveedor, PrecioUnitario FROM @Items;
        UPDATE compras.OrdenCompra SET Total =
            (SELECT COALESCE(SUM(Subtotal), 0) FROM compras.OrdenCompraDetalle WHERE IdOrdenCompra = @IdOrdenCompra)
        WHERE IdOrdenCompra = @IdOrdenCompra;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE [compras].[usp_OrdenCompra_ActualizarEstado]
    @IdOrdenCompra INT,
    @EstadoNuevo NVARCHAR(30),
    @IdUsuario INT = NULL,
    @Observacion NVARCHAR(250) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @EstadoNuevo = UPPER(LTRIM(RTRIM(@EstadoNuevo)));
    IF @EstadoNuevo NOT IN (N'APROBADA', N'ANULADA', N'CERRADA')
        THROW 50056, 'Estado inválido para la transición manual de OrdenCompra.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @EstadoAnterior NVARCHAR(30), @IdMoneda INT, @IdRequerimiento INT, @IdProyecto INT;
        SELECT @EstadoAnterior = oc.Estado, @IdMoneda = oc.IdMoneda,
            @IdRequerimiento = oc.IdRequerimiento, @IdProyecto = r.IdProyecto
        FROM compras.OrdenCompra oc WITH (UPDLOCK, HOLDLOCK)
        JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
        WHERE oc.IdOrdenCompra = @IdOrdenCompra;
        IF @EstadoAnterior IS NULL THROW 50057, 'Orden de compra no existe.', 1;
        IF @EstadoAnterior = @EstadoNuevo
        BEGIN
            COMMIT TRANSACTION;
            SELECT 1 AS Ok;
            RETURN;
        END;

        IF NOT ((@EstadoAnterior = 'REGISTRADA' AND @EstadoNuevo IN ('APROBADA', 'ANULADA'))
             OR (@EstadoAnterior = 'APROBADA' AND @EstadoNuevo = 'ANULADA')
             OR (@EstadoAnterior = 'ATENDIDA' AND @EstadoNuevo = 'CERRADA'))
            THROW 51440, 'TRANSICION_OC_INVALIDA.', 1;

        IF @EstadoNuevo = 'APROBADA'
        BEGIN
            IF EXISTS
            (
                SELECT 1
                FROM compras.OrdenCompraDetalle od
                LEFT JOIN compras.RequerimientoDetalle rd
                    ON rd.IdRequerimiento = @IdRequerimiento AND rd.IdMaterial = od.IdMaterial
                WHERE od.IdOrdenCompra = @IdOrdenCompra
                  AND (rd.IdRequerimientoDetalle IS NULL OR rd.IdPresupuestoDetalle IS NULL)
            )
                THROW 51433, 'PARTIDA_REQUERIDA: todas las líneas de la OC deben tener partida presupuestaria.', 1;

            DECLARE @Compromisos compras.TVP_MovimientoEconomico;
            INSERT INTO @Compromisos
                (IdPresupuestoDetalle, TipoMovimiento, ClaveEvento, Origen, IdOrigen, Monto, Fecha, Observacion)
            SELECT rd.IdPresupuestoDetalle, 'COMPROMISO',
                CONCAT('OC:', @IdOrdenCompra, ':APROBACION:MATERIAL:', od.IdMaterial),
                'ORDEN_COMPRA', @IdOrdenCompra, od.Subtotal, SYSDATETIME(),
                N'Compromiso por aprobación de OrdenCompra'
            FROM compras.OrdenCompraDetalle od
            JOIN compras.RequerimientoDetalle rd ON rd.IdRequerimiento = @IdRequerimiento
                AND rd.IdMaterial = od.IdMaterial
            WHERE od.IdOrdenCompra = @IdOrdenCompra;

            EXEC compras.usp_IntegracionEconomica_RegistrarLote
                @IdMoneda = @IdMoneda, @IdProyecto = @IdProyecto, @Movimientos = @Compromisos;
        END;

        IF @EstadoAnterior = 'APROBADA' AND @EstadoNuevo = 'ANULADA'
        BEGIN
            IF EXISTS (SELECT 1 FROM compras.Compra WHERE IdOrdenCompra = @IdOrdenCompra AND Estado = 'ACEPTADA')
                THROW 51441, 'OC_CON_COMPRA_ACEPTADA: no puede anularse.', 1;
            DECLARE @Liberaciones compras.TVP_MovimientoEconomico;
            INSERT INTO @Liberaciones
                (IdPresupuestoDetalle, TipoMovimiento, ClaveEvento, Origen, IdOrigen, Monto, Fecha, Observacion)
            SELECT rd.IdPresupuestoDetalle, 'LIBERACION',
                CONCAT('OC:', @IdOrdenCompra, ':ANULACION:MATERIAL:', od.IdMaterial),
                'ORDEN_COMPRA', @IdOrdenCompra, od.Subtotal, SYSDATETIME(),
                N'Liberación total por anulación de OrdenCompra'
            FROM compras.OrdenCompraDetalle od
            JOIN compras.RequerimientoDetalle rd ON rd.IdRequerimiento = @IdRequerimiento
                AND rd.IdMaterial = od.IdMaterial
            WHERE od.IdOrdenCompra = @IdOrdenCompra;
            EXEC compras.usp_IntegracionEconomica_RegistrarLote
                @IdMoneda = @IdMoneda, @IdProyecto = @IdProyecto, @Movimientos = @Liberaciones;
        END;

        UPDATE compras.OrdenCompra SET Estado = @EstadoNuevo WHERE IdOrdenCompra = @IdOrdenCompra;
        INSERT INTO compras.OrdenCompraHistorial
            (IdOrdenCompra, EstadoAnterior, EstadoNuevo, IdUsuario, Observacion)
        VALUES (@IdOrdenCompra, @EstadoAnterior, @EstadoNuevo, @IdUsuario, @Observacion);
        COMMIT TRANSACTION;
        SELECT 1 AS Ok;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
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

IF EXISTS (SELECT 1 FROM @Items WHERE Cantidad <= 0 OR PrecioUnitario <= 0)
    THROW 51442, 'Las cantidades y precios de la OC deben ser positivos.', 1;
IF EXISTS
(
    SELECT IdMaterial FROM @Items
    EXCEPT SELECT IdMaterial FROM compras.RequerimientoDetalle
        WHERE IdRequerimiento = @IdRequerimiento
)
    THROW 51443, 'MATERIAL_FUERA_REQUERIMIENTO: toda línea de OC debe provenir del Requerimiento.', 1;
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
'REGISTRADA',
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
IF EXISTS
(
    SELECT 1
    FROM @Items i
    LEFT JOIN ControlPresupuestario.PresupuestoDetalle pd
        ON pd.IdPresupuestoDetalle = i.IdPresupuestoDetalle
    LEFT JOIN ControlPresupuestario.PresupuestoVersion pv
        ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
    LEFT JOIN ControlPresupuestario.EstadoPresupuesto ep
        ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
    LEFT JOIN ControlPresupuestario.Presupuesto p
        ON p.IdPresupuesto = pv.IdPresupuesto
    LEFT JOIN ControlPresupuestario.CentroCosto cc
        ON cc.IdCentroCosto = p.IdCentroCosto
    WHERE i.IdPresupuestoDetalle IS NOT NULL
      AND (pd.IdPresupuestoDetalle IS NULL OR ep.Codigo <> 'APROBADO'
           OR p.Activo <> 1 OR cc.Activo <> 1 OR cc.IdProyecto <> @IdProyecto)
)
    THROW 51420, 'PARTIDA_REQUERIMIENTO_INVALIDA: debe pertenecer a la versión APROBADA vigente del CentroCosto del Proyecto.', 1;
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

IF EXISTS
(
    SELECT 1
    FROM @Items i
    LEFT JOIN ControlPresupuestario.PresupuestoDetalle pd
        ON pd.IdPresupuestoDetalle = i.IdPresupuestoDetalle
    LEFT JOIN ControlPresupuestario.PresupuestoVersion pv
        ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
    LEFT JOIN ControlPresupuestario.EstadoPresupuesto ep
        ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
    LEFT JOIN ControlPresupuestario.Presupuesto p
        ON p.IdPresupuesto = pv.IdPresupuesto
    LEFT JOIN ControlPresupuestario.CentroCosto cc
        ON cc.IdCentroCosto = p.IdCentroCosto
    WHERE i.IdPresupuestoDetalle IS NOT NULL
      AND (pd.IdPresupuestoDetalle IS NULL OR ep.Codigo <> 'APROBADO'
           OR p.Activo <> 1 OR cc.Activo <> 1 OR cc.IdProyecto <> @IdProyecto)
)
    THROW 51420, 'PARTIDA_REQUERIMIENTO_INVALIDA: debe pertenecer a la versión APROBADA vigente del CentroCosto del Proyecto.', 1;

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
