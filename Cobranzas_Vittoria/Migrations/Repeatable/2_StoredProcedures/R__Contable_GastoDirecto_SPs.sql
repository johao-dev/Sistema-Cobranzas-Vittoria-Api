CREATE OR ALTER PROCEDURE contable.usp_GastoDirecto_Listar
    @Estado VARCHAR(20) = NULL,
    @IdProveedor INT = NULL,
    @IdCentroCosto INT = NULL,
    @Desde DATE = NULL,
    @Hasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT gd.IdGastoDirecto, gd.IdPresupuestoDetalle, gd.IdProveedor,
        pr.RazonSocial AS Proveedor, gd.IdMoneda, m.Codigo AS Moneda,
        gd.Fecha, gd.Concepto, gd.Descripcion, gd.Monto, gd.Estado,
        gd.FechaCreacion, gd.FechaActualizacion,
        p.IdPresupuesto, p.Codigo AS CodigoPresupuesto,
        cc.IdCentroCosto, cc.Codigo AS CodigoCentroCosto, cc.Nombre AS CentroCosto,
        cc.IdProyecto, cp.IdCatalogoPartida, cp.Codigo AS CodigoPartida,
        cp.Nombre AS Partida,
        ISNULL(doc.TotalDocumentos, 0) AS TotalDocumentos
    FROM contable.GastoDirecto gd
    JOIN maestra.Moneda m ON m.IdMoneda = gd.IdMoneda
    LEFT JOIN maestra.Proveedor pr ON pr.IdProveedor = gd.IdProveedor
    JOIN ControlPresupuestario.PresupuestoDetalle pd
        ON pd.IdPresupuestoDetalle = gd.IdPresupuestoDetalle
    JOIN ControlPresupuestario.CatalogoPartida cp
        ON cp.IdCatalogoPartida = pd.IdCatalogoPartida
    JOIN ControlPresupuestario.PresupuestoVersion pv
        ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
    JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = pv.IdPresupuesto
    JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
    OUTER APPLY
    (
        SELECT COUNT(*) AS TotalDocumentos
        FROM contable.GastoDirectoDocumento d
        WHERE d.IdGastoDirecto = gd.IdGastoDirecto
    ) doc
    WHERE (@Estado IS NULL OR gd.Estado = @Estado)
      AND (@IdProveedor IS NULL OR gd.IdProveedor = @IdProveedor)
      AND (@IdCentroCosto IS NULL OR cc.IdCentroCosto = @IdCentroCosto)
      AND (@Desde IS NULL OR gd.Fecha >= @Desde)
      AND (@Hasta IS NULL OR gd.Fecha <= @Hasta)
    ORDER BY gd.Fecha DESC, gd.IdGastoDirecto DESC;
END;
GO

CREATE OR ALTER PROCEDURE contable.usp_GastoDirecto_Obtener
    @IdGastoDirecto INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT gd.IdGastoDirecto, gd.IdPresupuestoDetalle, gd.IdProveedor,
        pr.RazonSocial AS Proveedor, gd.IdMoneda, m.Codigo AS Moneda,
        gd.Fecha, gd.Concepto, gd.Descripcion, gd.Monto, gd.Estado,
        gd.FechaCreacion, gd.FechaActualizacion,
        p.IdPresupuesto, p.Codigo AS CodigoPresupuesto,
        cc.IdCentroCosto, cc.Codigo AS CodigoCentroCosto, cc.Nombre AS CentroCosto,
        cc.IdProyecto, cp.IdCatalogoPartida, cp.Codigo AS CodigoPartida,
        cp.Nombre AS Partida
    FROM contable.GastoDirecto gd
    JOIN maestra.Moneda m ON m.IdMoneda = gd.IdMoneda
    LEFT JOIN maestra.Proveedor pr ON pr.IdProveedor = gd.IdProveedor
    JOIN ControlPresupuestario.PresupuestoDetalle pd
        ON pd.IdPresupuestoDetalle = gd.IdPresupuestoDetalle
    JOIN ControlPresupuestario.CatalogoPartida cp
        ON cp.IdCatalogoPartida = pd.IdCatalogoPartida
    JOIN ControlPresupuestario.PresupuestoVersion pv
        ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
    JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = pv.IdPresupuesto
    JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
    WHERE gd.IdGastoDirecto = @IdGastoDirecto;

    SELECT IdGastoDirectoDocumento, IdGastoDirecto, TipoDocumento,
        NombreArchivo, RutaArchivo, Extension, FechaCreacion
    FROM contable.GastoDirectoDocumento
    WHERE IdGastoDirecto = @IdGastoDirecto
    ORDER BY FechaCreacion DESC, IdGastoDirectoDocumento DESC;
END;
GO

CREATE OR ALTER PROCEDURE contable.usp_GastoDirecto_Crear
    @IdPresupuestoDetalle INT,
    @IdProveedor INT = NULL,
    @IdMoneda INT,
    @Fecha DATE,
    @Concepto NVARCHAR(250),
    @Descripcion NVARCHAR(500) = NULL,
    @Monto DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Concepto = NULLIF(LTRIM(RTRIM(@Concepto)), '');
    SET @Descripcion = NULLIF(LTRIM(RTRIM(@Descripcion)), '');
    IF @IdPresupuestoDetalle IS NULL OR @IdPresupuestoDetalle <= 0
        THROW 51500, 'CAMPO_REQUERIDO: IdPresupuestoDetalle.', 1;
    IF @IdMoneda IS NULL OR @IdMoneda <= 0
        THROW 51500, 'CAMPO_REQUERIDO: IdMoneda.', 1;
    IF @Fecha IS NULL THROW 51500, 'CAMPO_REQUERIDO: Fecha.', 1;
    IF @Concepto IS NULL THROW 51500, 'CAMPO_REQUERIDO: Concepto.', 1;
    IF @Monto IS NULL OR @Monto <= 0
        THROW 51501, 'MONTO_INVALIDO: Monto debe ser mayor a cero.', 1;
    IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle
                   WHERE IdPresupuestoDetalle = @IdPresupuestoDetalle)
        THROW 51502, 'PARTIDA_INVALIDA: PresupuestoDetalle no existe.', 1;
    IF NOT EXISTS (SELECT 1 FROM maestra.Moneda WHERE IdMoneda = @IdMoneda AND Activo = 1)
        THROW 51503, 'MONEDA_INVALIDA: la moneda no existe o está inactiva.', 1;
    IF @IdProveedor IS NOT NULL AND NOT EXISTS
       (SELECT 1 FROM maestra.Proveedor WHERE IdProveedor = @IdProveedor AND Activo = 1)
        THROW 51504, 'PROVEEDOR_INVALIDO: el proveedor no existe o está inactivo.', 1;

    INSERT INTO contable.GastoDirecto
        (IdPresupuestoDetalle, IdProveedor, IdMoneda, Fecha, Concepto,
         Descripcion, Monto, Estado)
    VALUES
        (@IdPresupuestoDetalle, @IdProveedor, @IdMoneda, @Fecha, @Concepto,
         @Descripcion, @Monto, 'REGISTRADO');
    SELECT CONVERT(INT, SCOPE_IDENTITY()) AS IdGastoDirecto;
END;
GO

CREATE OR ALTER PROCEDURE contable.usp_GastoDirecto_Actualizar
    @IdGastoDirecto INT,
    @IdPresupuestoDetalle INT,
    @IdProveedor INT = NULL,
    @IdMoneda INT,
    @Fecha DATE,
    @Concepto NVARCHAR(250),
    @Descripcion NVARCHAR(500) = NULL,
    @Monto DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Concepto = NULLIF(LTRIM(RTRIM(@Concepto)), '');
    SET @Descripcion = NULLIF(LTRIM(RTRIM(@Descripcion)), '');
    IF @Concepto IS NULL OR @Monto IS NULL OR @Monto <= 0
        THROW 51501, 'DATOS_INVALIDOS: Concepto y monto positivo son requeridos.', 1;
    IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle
                   WHERE IdPresupuestoDetalle = @IdPresupuestoDetalle)
        THROW 51502, 'PARTIDA_INVALIDA: PresupuestoDetalle no existe.', 1;
    IF NOT EXISTS (SELECT 1 FROM maestra.Moneda WHERE IdMoneda = @IdMoneda AND Activo = 1)
        THROW 51503, 'MONEDA_INVALIDA: la moneda no existe o está inactiva.', 1;
    IF @IdProveedor IS NOT NULL AND NOT EXISTS
       (SELECT 1 FROM maestra.Proveedor WHERE IdProveedor = @IdProveedor AND Activo = 1)
        THROW 51504, 'PROVEEDOR_INVALIDO: el proveedor no existe o está inactivo.', 1;

    UPDATE contable.GastoDirecto
    SET IdPresupuestoDetalle = @IdPresupuestoDetalle,
        IdProveedor = @IdProveedor, IdMoneda = @IdMoneda, Fecha = @Fecha,
        Concepto = @Concepto, Descripcion = @Descripcion, Monto = @Monto,
        FechaActualizacion = SYSUTCDATETIME()
    WHERE IdGastoDirecto = @IdGastoDirecto AND Estado = 'REGISTRADO';
    IF @@ROWCOUNT = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM contable.GastoDirecto WHERE IdGastoDirecto = @IdGastoDirecto)
            THROW 51505, 'GASTO_NO_EXISTE: no se encontró el gasto directo.', 1;
        THROW 51506, 'ESTADO_INVALIDO: sólo un gasto REGISTRADO puede editarse.', 1;
    END;
    SELECT @IdGastoDirecto AS IdGastoDirecto;
END;
GO

CREATE OR ALTER PROCEDURE contable.usp_GastoDirecto_Confirmar
    @IdGastoDirecto INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE BEGIN SAVE TRANSACTION GastoDirectoConfirmar; SET @SavepointCreado = 1; END;

        DECLARE @Estado VARCHAR(20), @IdDetalle INT, @IdMoneda INT,
            @Monto DECIMAL(18,2), @Fecha DATE, @IdPresupuesto INT,
            @IdPartida INT, @MontoPresupuestado DECIMAL(18,2),
            @CodigoEstado VARCHAR(30), @PresupuestoActivo BIT,
            @PartidaActiva BIT, @MonedaPresupuesto INT;
        SELECT @Estado = Estado, @IdDetalle = IdPresupuestoDetalle,
            @IdMoneda = IdMoneda, @Monto = Monto, @Fecha = Fecha
        FROM contable.GastoDirecto WITH (UPDLOCK, HOLDLOCK)
        WHERE IdGastoDirecto = @IdGastoDirecto;
        IF @Estado IS NULL THROW 51505, 'GASTO_NO_EXISTE: no se encontró el gasto directo.', 1;
        IF @Estado = 'ANULADO' THROW 51506, 'ESTADO_INVALIDO: un gasto ANULADO no puede confirmarse.', 1;

        SELECT @IdPresupuesto = pv.IdPresupuesto, @IdPartida = pd.IdCatalogoPartida,
            @MontoPresupuestado = pd.MontoPresupuestado, @CodigoEstado = ep.Codigo,
            @PresupuestoActivo = p.Activo, @PartidaActiva = cp.Activo,
            @MonedaPresupuesto = p.IdMoneda
        FROM ControlPresupuestario.PresupuestoDetalle pd WITH (HOLDLOCK)
        JOIN ControlPresupuestario.PresupuestoVersion pv WITH (HOLDLOCK)
            ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
        JOIN ControlPresupuestario.EstadoPresupuesto ep WITH (HOLDLOCK)
            ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
        JOIN ControlPresupuestario.Presupuesto p WITH (UPDLOCK, HOLDLOCK)
            ON p.IdPresupuesto = pv.IdPresupuesto
        JOIN ControlPresupuestario.CatalogoPartida cp WITH (HOLDLOCK)
            ON cp.IdCatalogoPartida = pd.IdCatalogoPartida
        WHERE pd.IdPresupuestoDetalle = @IdDetalle;
        IF @IdPresupuesto IS NULL THROW 51502, 'PARTIDA_INVALIDA: no existe.', 1;
        IF @Estado = 'REGISTRADO' AND @CodigoEstado <> 'APROBADO'
            THROW 51507, 'VERSION_NO_VIGENTE: un gasto nuevo requiere la versión APROBADO.', 1;
        IF @Estado = 'REGISTRADO' AND (@PresupuestoActivo <> 1 OR @PartidaActiva <> 1)
            THROW 51502, 'PARTIDA_INVALIDA: presupuesto o partida inactivos.', 1;
        IF @Estado = 'REGISTRADO' AND EXISTS
           (SELECT 1 FROM ControlPresupuestario.CatalogoPartida WITH (HOLDLOCK)
            WHERE IdPartidaPadre = @IdPartida AND Activo = 1)
            THROW 51502, 'PARTIDA_INVALIDA: sólo una partida hoja puede recibir ejecución.', 1;
        IF @Estado = 'REGISTRADO' AND @MonedaPresupuesto <> @IdMoneda
            THROW 51508, 'MONEDA_INCOMPATIBLE: gasto y presupuesto deben usar la misma moneda.', 1;

        IF @Estado = 'REGISTRADO'
        BEGIN
            DECLARE @ResultadoLock INT, @RecursoLock NVARCHAR(255) =
                CONCAT('CP:', @IdPresupuesto, ':PARTIDA:', @IdPartida);
            EXEC @ResultadoLock = sys.sp_getapplock @Resource = @RecursoLock,
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @ResultadoLock < 0
                THROW 51509, 'CONCURRENCIA: no se pudo serializar el saldo presupuestario.', 1;

            DECLARE @PresupuestadoVigente DECIMAL(18,2);
            SELECT @PresupuestadoVigente = pd.MontoPresupuestado
            FROM ControlPresupuestario.PresupuestoVersion pv WITH (HOLDLOCK)
            JOIN ControlPresupuestario.EstadoPresupuesto ep WITH (HOLDLOCK)
              ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto AND ep.Codigo = 'APROBADO'
            JOIN ControlPresupuestario.PresupuestoDetalle pd WITH (HOLDLOCK)
              ON pd.IdPresupuestoVersion = pv.IdPresupuestoVersion
             AND pd.IdCatalogoPartida = @IdPartida
            WHERE pv.IdPresupuesto = @IdPresupuesto;
            IF @PresupuestadoVigente IS NULL
                THROW 51507, 'VERSION_NO_VIGENTE: la partida no existe en la línea base aprobada.', 1;

            DECLARE @Comprometido DECIMAL(18,2), @Ejecutado DECIMAL(18,2);
            SELECT
                @Comprometido = COALESCE(SUM(CASE
                    WHEN tm.Codigo = 'COMPROMISO' THEN mp.Monto
                    WHEN tm.Codigo = 'LIBERACION' THEN -mp.Monto
                    WHEN tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'COMPROMISO' AND mp.Direccion = 'INCREMENTO' THEN mp.Monto
                    WHEN tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'COMPROMISO' AND mp.Direccion = 'DECREMENTO' THEN -mp.Monto ELSE 0 END), 0),
                @Ejecutado = COALESCE(SUM(CASE
                    WHEN tm.Codigo = 'EJECUCION' THEN mp.Monto
                    WHEN tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'EJECUCION' AND mp.Direccion = 'INCREMENTO' THEN mp.Monto
                    WHEN tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'EJECUCION' AND mp.Direccion = 'DECREMENTO' THEN -mp.Monto ELSE 0 END), 0)
            FROM ControlPresupuestario.MovimientoPresupuestal mp WITH (UPDLOCK, HOLDLOCK)
            JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
              ON tm.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
            JOIN ControlPresupuestario.PresupuestoDetalle pd
              ON pd.IdPresupuestoDetalle = mp.IdPresupuestoDetalle
            JOIN ControlPresupuestario.PresupuestoVersion pv
              ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
            WHERE pv.IdPresupuesto = @IdPresupuesto
              AND pd.IdCatalogoPartida = @IdPartida;
            IF @PresupuestadoVigente - @Comprometido - @Ejecutado - @Monto < 0
                THROW 51510, 'SALDO_INSUFICIENTE: la confirmación dejaría la partida excedida.', 1;
        END;

        DECLARE @Movimiento TABLE
        (
            IdMovimientoPresupuestal BIGINT, IdPresupuestoDetalle INT,
            IdTipoMovimientoPresupuestal INT, TipoMovimiento VARCHAR(30),
            ClaveEvento VARCHAR(200), Origen VARCHAR(50), IdOrigen INT,
            Afectacion VARCHAR(20), Direccion VARCHAR(20), Fecha DATETIME2(0),
            Monto DECIMAL(18,2), Observacion NVARCHAR(500), IdPresupuesto INT,
            IdPresupuestoVersion INT, IdMoneda INT, CodigoMoneda VARCHAR(3), EsNuevo BIT
        );
        DECLARE @ClaveConfirmacion VARCHAR(200) =
            CONCAT('CONTABLE:GASTO_DIRECTO:', @IdGastoDirecto, ':CONFIRMACION');
        INSERT INTO @Movimiento
        EXEC ControlPresupuestario.usp_MovimientoPresupuestal_Registrar
            @IdPresupuestoDetalle = @IdDetalle, @TipoMovimiento = 'EJECUCION',
            @ClaveEvento = @ClaveConfirmacion,
            @Origen = 'GASTO_DIRECTO', @IdOrigen = @IdGastoDirecto,
            @Monto = @Monto, @Fecha = @Fecha,
            @Observacion = N'Confirmación de gasto directo.';

        IF @Estado = 'REGISTRADO'
            UPDATE contable.GastoDirecto SET Estado = 'CONFIRMADO',
                FechaActualizacion = SYSUTCDATETIME()
            WHERE IdGastoDirecto = @IdGastoDirecto AND Estado = 'REGISTRADO';
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT gd.*, m.IdMovimientoPresupuestal, m.EsNuevo AS MovimientoCreado
        FROM contable.GastoDirecto gd CROSS JOIN @Movimiento m
        WHERE gd.IdGastoDirecto = @IdGastoDirecto;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION GastoDirectoConfirmar;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE contable.usp_GastoDirecto_Anular
    @IdGastoDirecto INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE BEGIN SAVE TRANSACTION GastoDirectoAnular; SET @SavepointCreado = 1; END;
        DECLARE @Estado VARCHAR(20), @IdDetalle INT, @Monto DECIMAL(18,2), @Fecha DATE;
        SELECT @Estado = Estado, @IdDetalle = IdPresupuestoDetalle,
            @Monto = Monto, @Fecha = Fecha
        FROM contable.GastoDirecto WITH (UPDLOCK, HOLDLOCK)
        WHERE IdGastoDirecto = @IdGastoDirecto;
        IF @Estado IS NULL THROW 51505, 'GASTO_NO_EXISTE: no se encontró el gasto directo.', 1;

        DECLARE @ClaveConfirmacion VARCHAR(200) =
            CONCAT('CONTABLE:GASTO_DIRECTO:', @IdGastoDirecto, ':CONFIRMACION');
        DECLARE @ClaveAnulacion VARCHAR(200) =
            CONCAT('CONTABLE:GASTO_DIRECTO:', @IdGastoDirecto, ':ANULACION');
        DECLARE @FueConfirmado BIT = CASE WHEN @Estado = 'CONFIRMADO' OR EXISTS
        (
            SELECT 1 FROM ControlPresupuestario.MovimientoPresupuestal
            WHERE ClaveEvento = @ClaveConfirmacion
        ) THEN 1 ELSE 0 END;

        IF @FueConfirmado = 1
        BEGIN
            DECLARE @Movimiento TABLE
            (
                IdMovimientoPresupuestal BIGINT, IdPresupuestoDetalle INT,
                IdTipoMovimientoPresupuestal INT, TipoMovimiento VARCHAR(30),
                ClaveEvento VARCHAR(200), Origen VARCHAR(50), IdOrigen INT,
                Afectacion VARCHAR(20), Direccion VARCHAR(20), Fecha DATETIME2(0),
                Monto DECIMAL(18,2), Observacion NVARCHAR(500), IdPresupuesto INT,
                IdPresupuestoVersion INT, IdMoneda INT, CodigoMoneda VARCHAR(3), EsNuevo BIT
            );
            INSERT INTO @Movimiento
            EXEC ControlPresupuestario.usp_MovimientoPresupuestal_Registrar
                @IdPresupuestoDetalle = @IdDetalle, @TipoMovimiento = 'AJUSTE',
                @ClaveEvento = @ClaveAnulacion,
                @Origen = 'GASTO_DIRECTO', @IdOrigen = @IdGastoDirecto,
                @Monto = @Monto, @Fecha = @Fecha,
                @Observacion = N'Anulación de gasto directo confirmado.',
                @Afectacion = 'EJECUCION', @Direccion = 'DECREMENTO';
        END;

        IF @Estado <> 'ANULADO'
            UPDATE contable.GastoDirecto SET Estado = 'ANULADO',
                FechaActualizacion = SYSUTCDATETIME()
            WHERE IdGastoDirecto = @IdGastoDirecto;
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT * FROM contable.GastoDirecto WHERE IdGastoDirecto = @IdGastoDirecto;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION GastoDirectoAnular;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE contable.usp_GastoDirectoDocumento_Registrar
    @IdGastoDirecto INT,
    @TipoDocumento VARCHAR(20),
    @NombreArchivo NVARCHAR(255),
    @RutaArchivo NVARCHAR(500),
    @Extension NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @TipoDocumento NOT IN ('Factura', 'Pago')
        THROW 51511, 'TIPO_DOCUMENTO_INVALIDO: use Factura o Pago.', 1;
    IF NOT EXISTS (SELECT 1 FROM contable.GastoDirecto WHERE IdGastoDirecto = @IdGastoDirecto)
        THROW 51505, 'GASTO_NO_EXISTE: no se encontró el gasto directo.', 1;
    INSERT INTO contable.GastoDirectoDocumento
        (IdGastoDirecto, TipoDocumento, NombreArchivo, RutaArchivo, Extension)
    VALUES
        (@IdGastoDirecto, @TipoDocumento, @NombreArchivo, @RutaArchivo, @Extension);
    SELECT CONVERT(INT, SCOPE_IDENTITY()) AS IdGastoDirectoDocumento;
END;
GO
