-- Definiciones anteriores a V2.8.2, solo para reproducir el upgrade en una base desechable.
-- Auxiliares internos: participan de la transacción y bloqueo del SP operativo.
CREATE OR ALTER PROCEDURE almacen.usp_StockProyecto_ExigirInicializacion
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM almacen.StockProyectoInicializacion WHERE Id = 1 AND FechaActivacion IS NOT NULL)
        THROW 51115, 'STOCK_PROYECTO_NO_INICIALIZADO: Almacén debe validar y cargar la distribución inicial antes de operar.', 1;
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_StockProyecto_ValidarCambios
    @Cambios almacen.TVP_StockProyectoCantidad READONLY
AS
BEGIN
    SET NOCOUNT ON;
    -- No iniciar ni confirmar otra transacción: global y distribución son una unidad.
    IF @@TRANCOUNT = 0 OR ISNULL(APPLOCK_MODE('public', 'Inventario:Movimientos', 'Transaction'), 'NoLock') <> 'Exclusive'
        THROW 51118, 'CONTEXTO_INVENTARIO_INVALIDO: Se requiere la transacción y el bloqueo exclusivo de inventario.', 1;

    EXEC almacen.usp_StockProyecto_ExigirInicializacion;

    IF EXISTS (
        SELECT 1 FROM @Cambios c
        LEFT JOIN almacen.StockProyecto s ON s.IdMaterial = c.IdMaterial AND s.IdProyecto = c.IdProyecto
        WHERE ISNULL(s.Stock, 0) + c.Cantidad < 0
    )
    BEGIN
        DECLARE @Detalle NVARCHAR(MAX);
        SELECT @Detalle = STRING_AGG(CONVERT(NVARCHAR(MAX), CONCAT(
            'idMaterial=', c.IdMaterial, ' idProyecto=', c.IdProyecto,
            ' disponible=', ISNULL(s.Stock, 0), ' solicitado=', -c.Cantidad)), N'; ')
        FROM @Cambios c
        LEFT JOIN almacen.StockProyecto s ON s.IdMaterial = c.IdMaterial AND s.IdProyecto = c.IdProyecto
        WHERE ISNULL(s.Stock, 0) + c.Cantidad < 0;
        DECLARE @Mensaje NVARCHAR(2048) = LEFT(CONCAT('STOCK_PROYECTO_INSUFICIENTE: ', @Detalle), 2048);
        THROW 51114, @Mensaje, 1;
    END;
END;
GO

-- Operación administrativa explícita de puesta en marcha. No se publica por HTTP.
-- Recibe la distribución COMPLETA validada por Almacén; no altera KardexStock.
CREATE OR ALTER PROCEDURE almacen.usp_StockProyecto_Inicializar
    @Distribucion almacen.TVP_StockProyectoCantidad READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRAN;
        DECLARE @Bloqueo INT;
        EXEC @Bloqueo = sys.sp_getapplock
            @Resource = N'Inventario:Movimientos', @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 10000;
        IF @Bloqueo < 0
            THROW 51113, 'INVENTARIO_OCUPADO: No se pudo obtener el bloqueo de inventario. Reintente la operación.', 1;
        IF EXISTS (SELECT 1 FROM almacen.StockProyectoInicializacion WHERE FechaActivacion IS NOT NULL)
            THROW 51117, 'STOCK_PROYECTO_YA_INICIALIZADO: La regularización inicial solo se permite una vez.', 1;
        IF EXISTS (SELECT 1 FROM @Distribucion WHERE Cantidad < 0 OR Cantidad > 9999999999999999.99)
            THROW 51103, 'CANTIDAD_INVALIDA: La distribución inicial debe contener cantidades no negativas dentro de DECIMAL(18,2).', 1;
        IF EXISTS (
            SELECT 1 FROM @Distribucion d
            LEFT JOIN maestra.Material m WITH (HOLDLOCK) ON m.IdMaterial = d.IdMaterial
            LEFT JOIN maestra.Proyecto p ON p.IdProyecto = d.IdProyecto
            WHERE m.IdMaterial IS NULL OR p.IdProyecto IS NULL
        )
            THROW 51101, 'FK_NO_EXISTE: Material o proyecto inexistente en la distribución inicial.', 1;

        DELETE FROM almacen.StockProyecto;
        INSERT INTO almacen.StockProyecto (IdMaterial, IdProyecto, Stock)
        SELECT IdMaterial, IdProyecto, Cantidad FROM @Distribucion;

        -- Incluir todo el global detecta también materiales omitidos en la carga.
        DECLARE @Afectados almacen.TVP_StockProyectoCantidad;
        INSERT INTO @Afectados
        SELECT IdMaterial, 0, 0 FROM almacen.KardexStock
        UNION
        SELECT IdMaterial, 0, 0 FROM @Distribucion;
        EXEC almacen.usp_StockProyecto_VerificarConsistencia @Afectados;

        IF NOT EXISTS (SELECT 1 FROM almacen.StockProyectoInicializacion WHERE Id = 1)
            INSERT INTO almacen.StockProyectoInicializacion (Id) VALUES (1);
        UPDATE almacen.StockProyectoInicializacion SET FechaActivacion = SYSDATETIME() WHERE Id = 1;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
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
