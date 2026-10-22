-- Auxiliares internos: participan de la transacción y bloqueo del SP operativo.
CREATE OR ALTER PROCEDURE almacen.usp_StockProyecto_ExigirInicializacion
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM almacen.StockProyectoInicializacion WHERE Id = 1 AND FechaActivacion IS NOT NULL)
        THROW 51115, 'STOCK_PROYECTO_NO_INICIALIZADO: Almacén debe validar y cargar la distribución inicial antes de operar.', 1;
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_StockProyecto_VerificarConsistencia
    @Cantidades almacen.TVP_StockProyectoCantidad READONLY
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM (SELECT DISTINCT IdMaterial FROM @Cantidades) c
        LEFT JOIN almacen.KardexStock k ON k.IdMaterial = c.IdMaterial
        OUTER APPLY (SELECT SUM(Stock) AS Stock FROM almacen.StockProyecto s WHERE s.IdMaterial = c.IdMaterial) distribuido
        WHERE ISNULL(k.Stock, 0) <> ISNULL(distribuido.Stock, 0)
           OR ISNULL(k.Stock, 0) < 0
    )
        THROW 51116, 'STOCK_PROYECTO_INCONSISTENTE: El stock global no coincide con la distribución por proyecto. Se revierte la operación.', 1;
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

CREATE OR ALTER PROCEDURE almacen.usp_StockProyecto_AplicarCambios
    @Cambios almacen.TVP_StockProyectoCantidad READONLY
AS
BEGIN
    SET NOCOUNT ON;
    EXEC almacen.usp_StockProyecto_ValidarCambios @Cambios;

    UPDATE s SET Stock = s.Stock + c.Cantidad, FechaMovimiento = SYSDATETIME()
    FROM almacen.StockProyecto s
    JOIN @Cambios c ON c.IdMaterial = s.IdMaterial AND c.IdProyecto = s.IdProyecto;

    INSERT INTO almacen.StockProyecto (IdMaterial, IdProyecto, Stock)
    SELECT c.IdMaterial, c.IdProyecto, c.Cantidad FROM @Cambios c
    WHERE NOT EXISTS (SELECT 1 FROM almacen.StockProyecto s WHERE s.IdMaterial = c.IdMaterial AND s.IdProyecto = c.IdProyecto);

    EXEC almacen.usp_StockProyecto_VerificarConsistencia @Cambios;
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

CREATE OR ALTER PROCEDURE almacen.usp_StockProyecto_Listar
    @IdProyecto INT = NULL,
    @IdEspecialidad INT = NULL,
    @IdMaterial INT = NULL,
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.IdStockProyecto, s.IdProyecto, p.NombreProyecto AS Proyecto,
        s.IdMaterial, m.Codigo AS CodigoMaterial, m.Descripcion AS Nombre, m.UnidadMedida,
        m.IdEspecialidad, e.Nombre AS Especialidad, s.Stock,
        s.FechaMovimiento AS FechaUltimoMovimiento
    FROM almacen.StockProyecto s
    JOIN maestra.Proyecto p ON p.IdProyecto = s.IdProyecto
    JOIN maestra.Material m ON m.IdMaterial = s.IdMaterial
    JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
    WHERE (@IdProyecto IS NULL OR s.IdProyecto = @IdProyecto)
      AND (@IdEspecialidad IS NULL OR m.IdEspecialidad = @IdEspecialidad)
      AND (@IdMaterial IS NULL OR s.IdMaterial = @IdMaterial)
      AND (@FechaDesde IS NULL OR s.FechaMovimiento >= @FechaDesde)
      AND (@FechaHasta IS NULL OR CONVERT(DATE, s.FechaMovimiento) <= @FechaHasta)
    ORDER BY p.NombreProyecto, e.Nombre, m.Descripcion;
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_TransferenciaProyecto_Listar
    @IdTransferencia INT = NULL,
    @IdProyectoOrigen INT = NULL,
    @IdProyectoDestino INT = NULL,
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.IdTransferencia, t.IdProyectoOrigen, origen.NombreProyecto AS ProyectoOrigen,
        t.IdProyectoDestino, destino.NombreProyecto AS ProyectoDestino, t.Fecha, t.Observacion,
        d.IdTransferenciaProyectoDetalle, d.IdMaterial, m.Codigo AS CodigoMaterial,
        m.Descripcion AS Nombre, m.UnidadMedida, m.IdEspecialidad, e.Nombre AS Especialidad, d.Cantidad
    FROM almacen.TransferenciaProyecto t
    JOIN almacen.TransferenciaProyectoDetalle d ON d.IdTransferencia = t.IdTransferencia
    JOIN maestra.Proyecto origen ON origen.IdProyecto = t.IdProyectoOrigen
    JOIN maestra.Proyecto destino ON destino.IdProyecto = t.IdProyectoDestino
    JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
    JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
    WHERE (@IdTransferencia IS NULL OR t.IdTransferencia = @IdTransferencia)
      AND (@IdProyectoOrigen IS NULL OR t.IdProyectoOrigen = @IdProyectoOrigen)
      AND (@IdProyectoDestino IS NULL OR t.IdProyectoDestino = @IdProyectoDestino)
      AND (@FechaDesde IS NULL OR t.Fecha >= @FechaDesde)
      AND (@FechaHasta IS NULL OR t.Fecha <= @FechaHasta)
    ORDER BY t.Fecha DESC, t.IdTransferencia DESC, d.IdTransferenciaProyectoDetalle;
END;
GO

CREATE OR ALTER PROCEDURE almacen.usp_TransferenciaProyecto_Registrar
    @IdProyectoOrigen INT,
    @IdProyectoDestino INT,
    @Fecha DATE,
    @Observacion NVARCHAR(500),
    @Items almacen.TVP_TransferenciaProyectoItem READONLY
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
        EXEC almacen.usp_StockProyecto_ExigirInicializacion;

        IF @IdProyectoOrigen IS NULL OR @IdProyectoDestino IS NULL OR @Fecha IS NULL OR @Observacion IS NULL
            THROW 51100, 'CAMPO_REQUERIDO: origen, destino, fecha y observacion son obligatorios.', 1;
        IF @IdProyectoOrigen = @IdProyectoDestino
            THROW 51120, 'PROYECTOS_IGUALES: El proyecto origen y destino deben ser distintos.', 1;
        IF NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = @IdProyectoOrigen)
            OR NOT EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto = @IdProyectoDestino)
            THROW 51101, 'FK_NO_EXISTE: Proyecto origen o destino inexistente.', 1;
        IF NOT EXISTS (SELECT 1 FROM @Items)
            THROW 51100, 'CAMPO_REQUERIDO: items (debe tener al menos uno)', 1;
        IF EXISTS (SELECT 1 FROM @Items WHERE Cantidad <= 0)
            THROW 51103, 'CANTIDAD_INVALIDA: Las cantidades transferidas deben ser mayores a cero.', 1;
        IF EXISTS (
            SELECT 1 FROM @Items i LEFT JOIN maestra.Material m WITH (HOLDLOCK) ON m.IdMaterial = i.IdMaterial
            WHERE m.IdMaterial IS NULL
        )
            THROW 51101, 'FK_NO_EXISTE: Material inexistente.', 1;

        DECLARE @Consolidados TABLE (IdMaterial INT PRIMARY KEY, Cantidad DECIMAL(29,2));
        INSERT INTO @Consolidados SELECT IdMaterial, SUM(Cantidad) FROM @Items GROUP BY IdMaterial;
        IF EXISTS (SELECT 1 FROM @Consolidados WHERE Cantidad > 9999999999999999.99)
            THROW 51103, 'CANTIDAD_INVALIDA: La cantidad consolidada excede la capacidad del detalle.', 1;

        DECLARE @Cambios almacen.TVP_StockProyectoCantidad;
        INSERT INTO @Cambios SELECT IdMaterial, @IdProyectoOrigen, -Cantidad FROM @Consolidados;
        INSERT INTO @Cambios SELECT IdMaterial, @IdProyectoDestino, Cantidad FROM @Consolidados;
        EXEC almacen.usp_StockProyecto_AplicarCambios @Cambios;

        INSERT INTO almacen.TransferenciaProyecto (IdProyectoOrigen, IdProyectoDestino, Fecha, Observacion)
        VALUES (@IdProyectoOrigen, @IdProyectoDestino, @Fecha, @Observacion);
        DECLARE @IdTransferencia INT = CONVERT(INT, SCOPE_IDENTITY());
        INSERT INTO almacen.TransferenciaProyectoDetalle (IdTransferencia, IdMaterial, Cantidad)
        SELECT @IdTransferencia, IdMaterial, Cantidad FROM @Consolidados;

        -- Incluye errores de persistencia/trigger antes de confirmar.
        EXEC almacen.usp_StockProyecto_VerificarConsistencia @Cambios;
        COMMIT;
        EXEC almacen.usp_TransferenciaProyecto_Listar @IdTransferencia = @IdTransferencia;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO
