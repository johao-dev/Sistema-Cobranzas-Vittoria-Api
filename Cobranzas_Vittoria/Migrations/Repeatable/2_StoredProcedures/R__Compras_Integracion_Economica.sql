CREATE OR ALTER PROCEDURE compras.usp_IntegracionEconomica_RegistrarLote
    @IdMoneda INT,
    @IdProyecto INT,
    @Movimientos compras.TVP_MovimientoEconomico READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF XACT_STATE() = -1
        THROW 51410, 'TRANSACCION_NO_CONFIRMABLE: la integración económica requiere rollback.', 1;
    IF @IdMoneda IS NULL OR @IdMoneda <= 0
        THROW 51411, 'MONEDA_OC_INVALIDA: IdMoneda es requerido.', 1;
    IF @IdProyecto IS NULL OR @IdProyecto <= 0
        THROW 51412, 'PROYECTO_REQUERIDO: IdProyecto es requerido.', 1;
    IF EXISTS (SELECT 1 FROM @Movimientos WHERE Monto <= 0)
        THROW 51413, 'MONTO_ECONOMICO_INVALIDO: todos los movimientos deben ser positivos.', 1;
    IF EXISTS (SELECT 1 FROM @Movimientos WHERE TipoMovimiento NOT IN ('COMPROMISO', 'LIBERACION', 'EJECUCION'))
        THROW 51414, 'TIPO_MOVIMIENTO_ECONOMICO_INVALIDO.', 1;
    IF EXISTS (SELECT ClaveEvento FROM @Movimientos GROUP BY ClaveEvento HAVING COUNT(*) > 1)
        THROW 51415, 'CLAVE_EVENTO_DUPLICADA_EN_LOTE.', 1;

    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION ComprasIntegracionLote;
            SET @SavepointCreado = 1;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM @Movimientos m
            LEFT JOIN ControlPresupuestario.PresupuestoDetalle pd
                ON pd.IdPresupuestoDetalle = m.IdPresupuestoDetalle
            LEFT JOIN ControlPresupuestario.PresupuestoVersion pv
                ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
            LEFT JOIN ControlPresupuestario.EstadoPresupuesto ep
                ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
            LEFT JOIN ControlPresupuestario.Presupuesto p
                ON p.IdPresupuesto = pv.IdPresupuesto
            LEFT JOIN ControlPresupuestario.CentroCosto cc
                ON cc.IdCentroCosto = p.IdCentroCosto
            WHERE pd.IdPresupuestoDetalle IS NULL
               OR ep.Codigo NOT IN ('APROBADO', 'HISTORICO')
               OR p.Activo <> 1
               OR p.IdMoneda <> @IdMoneda
               OR cc.IdProyecto IS NULL
               OR cc.IdProyecto <> @IdProyecto
        )
            THROW 51416, 'PARTIDA_NO_COMPATIBLE: debe pertenecer al Proyecto, tener la moneda de la OC y provenir de una versión APROBADA/HISTORICO.', 1;

        DECLARE @Identidades TABLE
        (
            IdPresupuesto INT NOT NULL,
            IdCatalogoPartida INT NOT NULL,
            PRIMARY KEY (IdPresupuesto, IdCatalogoPartida)
        );

        INSERT INTO @Identidades (IdPresupuesto, IdCatalogoPartida)
        SELECT DISTINCT pv.IdPresupuesto, pd.IdCatalogoPartida
        FROM @Movimientos m
        JOIN ControlPresupuestario.PresupuestoDetalle pd
            ON pd.IdPresupuestoDetalle = m.IdPresupuestoDetalle
        JOIN ControlPresupuestario.PresupuestoVersion pv
            ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion;

        DECLARE @IdPresupuesto INT, @IdCatalogoPartida INT, @ResultadoLock INT,
            @RecursoLock NVARCHAR(255);
        DECLARE identidades CURSOR LOCAL FAST_FORWARD FOR
            SELECT IdPresupuesto, IdCatalogoPartida FROM @Identidades
            ORDER BY IdPresupuesto, IdCatalogoPartida;
        OPEN identidades;
        FETCH NEXT FROM identidades INTO @IdPresupuesto, @IdCatalogoPartida;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @RecursoLock = CONCAT('CP:', @IdPresupuesto, ':PARTIDA:', @IdCatalogoPartida);
            EXEC @ResultadoLock = sys.sp_getapplock
                @Resource = @RecursoLock,
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @ResultadoLock < 0
                THROW 51417, 'No se pudo serializar la validación de saldo presupuestario.', 1;
            FETCH NEXT FROM identidades INTO @IdPresupuesto, @IdCatalogoPartida;
        END;
        CLOSE identidades;
        DEALLOCATE identidades;

        SET @IdPresupuesto = NULL;
        ;WITH IdentidadMovimiento AS
        (
            SELECT m.TipoMovimiento, m.Monto, pv.IdPresupuesto, pd.IdCatalogoPartida
            FROM @Movimientos m
            JOIN ControlPresupuestario.PresupuestoDetalle pd
                ON pd.IdPresupuestoDetalle = m.IdPresupuestoDetalle
            JOIN ControlPresupuestario.PresupuestoVersion pv
                ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
        ), ImpactoNuevo AS
        (
            SELECT IdPresupuesto, IdCatalogoPartida,
                SUM(CASE TipoMovimiento
                    WHEN 'LIBERACION' THEN Monto
                    WHEN 'COMPROMISO' THEN -Monto
                    WHEN 'EJECUCION' THEN -Monto END) AS Impacto
            FROM IdentidadMovimiento
            GROUP BY IdPresupuesto, IdCatalogoPartida
        ), Vigente AS
        (
            SELECT pv.IdPresupuesto, pd.IdCatalogoPartida, pd.MontoPresupuestado
            FROM ControlPresupuestario.PresupuestoVersion pv
            JOIN ControlPresupuestario.EstadoPresupuesto ep
                ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto AND ep.Codigo = 'APROBADO'
            JOIN ControlPresupuestario.PresupuestoDetalle pd
                ON pd.IdPresupuestoVersion = pv.IdPresupuestoVersion
        ), Acumulado AS
        (
            SELECT pv.IdPresupuesto, pd.IdCatalogoPartida,
                SUM(CASE WHEN tm.Codigo = 'COMPROMISO' THEN mp.Monto ELSE 0 END)
              - SUM(CASE WHEN tm.Codigo = 'LIBERACION' THEN mp.Monto ELSE 0 END)
              + SUM(CASE WHEN tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'COMPROMISO' AND mp.Direccion = 'INCREMENTO' THEN mp.Monto ELSE 0 END)
              - SUM(CASE WHEN tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'COMPROMISO' AND mp.Direccion = 'DECREMENTO' THEN mp.Monto ELSE 0 END)
                AS Comprometido,
                SUM(CASE WHEN tm.Codigo = 'EJECUCION' THEN mp.Monto ELSE 0 END)
              + SUM(CASE WHEN tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'EJECUCION' AND mp.Direccion = 'INCREMENTO' THEN mp.Monto ELSE 0 END)
              - SUM(CASE WHEN tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'EJECUCION' AND mp.Direccion = 'DECREMENTO' THEN mp.Monto ELSE 0 END)
                AS Ejecutado
            FROM ControlPresupuestario.MovimientoPresupuestal mp WITH (UPDLOCK, HOLDLOCK)
            JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
                ON tm.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
            JOIN ControlPresupuestario.PresupuestoDetalle pd
                ON pd.IdPresupuestoDetalle = mp.IdPresupuestoDetalle
            JOIN ControlPresupuestario.PresupuestoVersion pv
                ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
            JOIN @Identidades i ON i.IdPresupuesto = pv.IdPresupuesto
                AND i.IdCatalogoPartida = pd.IdCatalogoPartida
            GROUP BY pv.IdPresupuesto, pd.IdCatalogoPartida
        )
        SELECT TOP (1) @IdPresupuesto = i.IdPresupuesto
        FROM @Identidades i
        LEFT JOIN Vigente v ON v.IdPresupuesto = i.IdPresupuesto
            AND v.IdCatalogoPartida = i.IdCatalogoPartida
        LEFT JOIN Acumulado a ON a.IdPresupuesto = i.IdPresupuesto
            AND a.IdCatalogoPartida = i.IdCatalogoPartida
        JOIN ImpactoNuevo n ON n.IdPresupuesto = i.IdPresupuesto
            AND n.IdCatalogoPartida = i.IdCatalogoPartida
        WHERE COALESCE(v.MontoPresupuestado, 0)
            - COALESCE(a.Comprometido, 0)
            - COALESCE(a.Ejecutado, 0)
            + n.Impacto < 0;

        IF @IdPresupuesto IS NOT NULL
            THROW 51418, 'SALDO_PRESUPUESTARIO_INSUFICIENTE: la operación dejaría una partida excedida.', 1;

        DECLARE @IdDetalle INT, @Tipo VARCHAR(30), @Clave VARCHAR(200),
            @Origen VARCHAR(50), @IdOrigen INT, @Monto DECIMAL(18,2),
            @Fecha DATETIME2(0), @Observacion NVARCHAR(500);
        DECLARE @ResultadoMovimiento TABLE
        (
            IdMovimientoPresupuestal BIGINT, IdPresupuestoDetalle INT,
            IdTipoMovimientoPresupuestal INT, TipoMovimiento VARCHAR(30),
            ClaveEvento VARCHAR(200), Origen VARCHAR(50), IdOrigen INT,
            Afectacion VARCHAR(20) NULL, Direccion VARCHAR(20) NULL,
            Fecha DATETIME2(0), Monto DECIMAL(18,2), Observacion NVARCHAR(500) NULL,
            IdPresupuesto INT, IdPresupuestoVersion INT, IdMoneda INT,
            CodigoMoneda VARCHAR(3), EsNuevo BIT
        );
        DECLARE movimientos CURSOR LOCAL FAST_FORWARD FOR
            SELECT IdPresupuestoDetalle, TipoMovimiento, ClaveEvento, Origen,
                IdOrigen, Monto, Fecha, Observacion
            FROM @Movimientos ORDER BY ClaveEvento;
        OPEN movimientos;
        FETCH NEXT FROM movimientos INTO @IdDetalle, @Tipo, @Clave, @Origen,
            @IdOrigen, @Monto, @Fecha, @Observacion;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            DELETE FROM @ResultadoMovimiento;
            INSERT INTO @ResultadoMovimiento
            EXEC ControlPresupuestario.usp_MovimientoPresupuestal_Registrar
                @IdPresupuestoDetalle = @IdDetalle, @TipoMovimiento = @Tipo,
                @ClaveEvento = @Clave, @Origen = @Origen, @IdOrigen = @IdOrigen,
                @Monto = @Monto, @Fecha = @Fecha, @Observacion = @Observacion;
            FETCH NEXT FROM movimientos INTO @IdDetalle, @Tipo, @Clave, @Origen,
                @IdOrigen, @Monto, @Fecha, @Observacion;
        END;
        CLOSE movimientos;
        DEALLOCATE movimientos;

        IF @TranCount = 0 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION ComprasIntegracionLote;
        THROW;
    END CATCH;
END;
GO
