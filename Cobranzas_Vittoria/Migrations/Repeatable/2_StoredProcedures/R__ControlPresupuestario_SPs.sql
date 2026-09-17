-- =============================================
-- Author:      Johao Bravo
-- Create date: 2026-09-17
-- Description: Procedimientos almacenados para el control presupuestario.
-- =============================================

/*
Control Presupuestario — núcleo de escritura.

Los escritores del mismo presupuesto deben bloquear primero Presupuesto.
Los SP participan en transacciones externas sin confirmar ni revertir por
completo una unidad que no les pertenece. Una transacción externa invalidada
por XACT_ABORT debe ser revertida por su propietario.

Aprobar comprueba cobertura del ledger. Operaciones pendientes con neto cero
se integrarán posteriormente con Compras/Contabilidad; no se infieren aquí.
Registrar valida AJUSTE defensivamente, sin agregar un CHECK al schema.
*/

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_Presupuesto_Crear
    @IdCentroCosto INT,
    @IdMoneda INT,
    @Codigo VARCHAR(50),
    @Nombre NVARCHAR(200),
    @Descripcion NVARCHAR(500) = NULL,
    @FechaInicio DATE = NULL,
    @FechaFin DATE = NULL,
    @UsuarioCreacion NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;

    DECLARE @TranCount INT = @@TRANCOUNT;
    DECLARE @SavepointCreado BIT = 0;

    BEGIN TRY
        SET @Codigo = LTRIM(RTRIM(@Codigo));
        SET @Nombre = LTRIM(RTRIM(@Nombre));

        IF @IdCentroCosto IS NULL OR @IdCentroCosto <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdCentroCosto positivo.', 1;
        IF @IdMoneda IS NULL OR @IdMoneda <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdMoneda positivo.', 1;
        IF NULLIF(@Codigo, '') IS NULL
            THROW 51200, 'CAMPO_REQUERIDO: Codigo.', 1;
        IF NULLIF(@Nombre, N'') IS NULL
            THROW 51200, 'CAMPO_REQUERIDO: Nombre.', 1;
        IF @FechaInicio IS NOT NULL AND @FechaFin IS NOT NULL AND @FechaFin < @FechaInicio
            THROW 51206, 'RANGO_FECHAS_INVALIDO: FechaFin es anterior a FechaInicio.', 1;

        IF @TranCount = 0
            BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_PresupuestoCrear;
            SET @SavepointCreado = 1;
        END;

        DECLARE @CentroActivo BIT;
        SELECT @CentroActivo = Activo
        FROM ControlPresupuestario.CentroCosto WITH (HOLDLOCK)
        WHERE IdCentroCosto = @IdCentroCosto;

        IF @CentroActivo IS NULL
            THROW 51201, 'REFERENCIA_NO_EXISTE: CentroCosto.', 1;
        IF @CentroActivo = 0
            THROW 51202, 'RECURSO_INACTIVO: CentroCosto.', 1;

        DECLARE @MonedaActiva BIT;
        DECLARE @CodigoMoneda VARCHAR(3);
        SELECT @MonedaActiva = Activo, @CodigoMoneda = Codigo
        FROM ControlPresupuestario.Moneda WITH (HOLDLOCK)
        WHERE IdMoneda = @IdMoneda;

        IF @MonedaActiva IS NULL
            THROW 51201, 'REFERENCIA_NO_EXISTE: Moneda.', 1;
        IF @MonedaActiva = 0
            THROW 51202, 'RECURSO_INACTIVO: Moneda.', 1;

        DECLARE @IdBorrador INT;
        SELECT @IdBorrador = IdEstadoPresupuesto
        FROM ControlPresupuestario.EstadoPresupuesto WITH (HOLDLOCK)
        WHERE Codigo = 'BORRADOR' AND Activo = 1;

        IF @IdBorrador IS NULL
            THROW 51204, 'CATALOGO_ESTRUCTURAL_INVALIDO: BORRADOR no disponible.', 1;

        -- HOLDLOCK protege también el código inexistente hasta insertar.
        IF EXISTS
        (
            SELECT 1
            FROM ControlPresupuestario.Presupuesto WITH
                (UPDLOCK, HOLDLOCK, INDEX(UQ_Presupuesto_Codigo))
            WHERE Codigo = @Codigo
        )
            THROW 51205, 'CODIGO_DUPLICADO: el presupuesto ya está registrado.', 1;

        DECLARE @FechaCreacion DATETIME2(0) = SYSUTCDATETIME();
        DECLARE @IdPresupuesto INT;
        DECLARE @IdPresupuestoVersion INT;

        INSERT INTO ControlPresupuestario.Presupuesto
            (IdCentroCosto, IdMoneda, Codigo, Nombre, Descripcion,
             FechaInicio, FechaFin, FechaCreacion)
        VALUES
            (@IdCentroCosto, @IdMoneda, @Codigo, @Nombre, @Descripcion,
             @FechaInicio, @FechaFin, @FechaCreacion);
        SET @IdPresupuesto = CONVERT(INT, SCOPE_IDENTITY());

        INSERT INTO ControlPresupuestario.PresupuestoVersion
            (IdPresupuesto, IdEstadoPresupuesto, NumeroVersion, FechaCreacion, UsuarioCreacion)
        VALUES
            (@IdPresupuesto, @IdBorrador, 1, @FechaCreacion, @UsuarioCreacion);
        SET @IdPresupuestoVersion = CONVERT(INT, SCOPE_IDENTITY());

        IF @TranCount = 0
            COMMIT TRANSACTION;

        SELECT @IdPresupuesto AS IdPresupuesto,
            @Codigo AS CodigoPresupuesto,
            @IdPresupuestoVersion AS IdPresupuestoVersion,
            CONVERT(INT, 1) AS NumeroVersion,
            CONVERT(VARCHAR(30), 'BORRADOR') AS EstadoPresupuesto,
            @IdMoneda AS IdMoneda,
            @CodigoMoneda AS CodigoMoneda,
            @FechaCreacion AS FechaCreacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_PresupuestoCrear;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_PresupuestoVersion_Aprobar
    @IdPresupuestoVersion INT,
    @UsuarioAprobacion NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;

    DECLARE @TranCount INT = @@TRANCOUNT;
    DECLARE @SavepointCreado BIT = 0;

    BEGIN TRY
        IF @IdPresupuestoVersion IS NULL OR @IdPresupuestoVersion <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuestoVersion positivo.', 1;

        IF @TranCount = 0
            BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_VersionAprobar;
            SET @SavepointCreado = 1;
        END;

        -- Lectura de localización. Se relee el objetivo tras tomar el mutex.
        DECLARE @IdPresupuesto INT;
        SELECT @IdPresupuesto = IdPresupuesto
        FROM ControlPresupuestario.PresupuestoVersion
        WHERE IdPresupuestoVersion = @IdPresupuestoVersion;

        IF @IdPresupuesto IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoVersion.', 1;

        DECLARE @PresupuestoActivo BIT;
        DECLARE @IdMoneda INT;
        SELECT @PresupuestoActivo = Activo, @IdMoneda = IdMoneda
        FROM ControlPresupuestario.Presupuesto WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Presupuesto))
        WHERE IdPresupuesto = @IdPresupuesto;

        IF @PresupuestoActivo IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: Presupuesto.', 1;
        IF @PresupuestoActivo = 0
            THROW 51202, 'RECURSO_INACTIVO: Presupuesto.', 1;

        DECLARE @MonedaActiva BIT;
        SELECT @MonedaActiva = Activo
        FROM ControlPresupuestario.Moneda WITH (HOLDLOCK)
        WHERE IdMoneda = @IdMoneda;

        IF @MonedaActiva IS NULL
            THROW 51204, 'CATALOGO_ESTRUCTURAL_INVALIDO: Moneda del presupuesto.', 1;
        IF @MonedaActiva = 0
            THROW 51202, 'RECURSO_INACTIVO: Moneda.', 1;

        DECLARE @IdBorrador INT;
        DECLARE @IdAprobado INT;
        DECLARE @IdHistorico INT;
        SELECT
            @IdBorrador = MAX(CASE WHEN Codigo = 'BORRADOR' THEN IdEstadoPresupuesto END),
            @IdAprobado = MAX(CASE WHEN Codigo = 'APROBADO' THEN IdEstadoPresupuesto END),
            @IdHistorico = MAX(CASE WHEN Codigo = 'HISTORICO' THEN IdEstadoPresupuesto END)
        FROM ControlPresupuestario.EstadoPresupuesto WITH (HOLDLOCK)
        WHERE Codigo IN ('BORRADOR', 'APROBADO', 'HISTORICO') AND Activo = 1;

        IF @IdBorrador IS NULL OR @IdAprobado IS NULL OR @IdHistorico IS NULL
            THROW 51204, 'CATALOGO_ESTRUCTURAL_INVALIDO: estados de aprobación no disponibles.', 1;

        DECLARE @IdEstadoObjetivo INT;
        DECLARE @NumeroVersion INT;
        SELECT @IdEstadoObjetivo = IdEstadoPresupuesto, @NumeroVersion = NumeroVersion
        FROM ControlPresupuestario.PresupuestoVersion WITH (UPDLOCK, HOLDLOCK)
        WHERE IdPresupuestoVersion = @IdPresupuestoVersion AND IdPresupuesto = @IdPresupuesto;

        IF @IdEstadoObjetivo IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoVersion objetivo.', 1;
        IF @IdEstadoObjetivo <> @IdBorrador
            THROW 51207, 'ESTADO_INVALIDO: solo puede aprobarse BORRADOR.', 1;

        DECLARE @CantidadAprobadas INT;
        DECLARE @CantidadBorradores INT;
        DECLARE @CantidadHistoricas INT;
        DECLARE @IdVersionAnterior INT;
        SELECT
            @CantidadAprobadas = COUNT(CASE WHEN IdEstadoPresupuesto = @IdAprobado THEN 1 END),
            @CantidadBorradores = COUNT(CASE WHEN IdEstadoPresupuesto = @IdBorrador THEN 1 END),
            @CantidadHistoricas = COUNT(CASE WHEN IdEstadoPresupuesto = @IdHistorico THEN 1 END),
            @IdVersionAnterior = MAX(CASE WHEN IdEstadoPresupuesto = @IdAprobado
                THEN IdPresupuestoVersion END)
        FROM ControlPresupuestario.PresupuestoVersion WITH (UPDLOCK, HOLDLOCK)
        WHERE IdPresupuesto = @IdPresupuesto;

        IF @CantidadAprobadas > 1 OR @CantidadBorradores <> 1
            THROW 51208, 'VERSIONES_INCONSISTENTES: se requiere un único BORRADOR y como máximo una APROBADO.', 1;
        IF @CantidadAprobadas = 0 AND (@CantidadHistoricas > 0 OR @NumeroVersion <> 1)
            THROW 51208, 'VERSIONES_INCONSISTENTES: no hay línea base vigente para esta revisión.', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle WITH (HOLDLOCK)
            WHERE IdPresupuestoVersion = @IdPresupuestoVersion
        ) OR EXISTS
        (
            SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle WITH (HOLDLOCK)
            WHERE IdPresupuestoVersion = @IdPresupuestoVersion AND MontoPresupuestado < 0
        ) OR EXISTS
        (
            SELECT IdCatalogoPartida
            FROM ControlPresupuestario.PresupuestoDetalle WITH (HOLDLOCK)
            WHERE IdPresupuestoVersion = @IdPresupuestoVersion
            GROUP BY IdCatalogoPartida HAVING COUNT(*) > 1
        )
            THROW 51209, 'SNAPSHOT_INVALIDO: se requieren detalles válidos y sin partidas duplicadas.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM ControlPresupuestario.PresupuestoDetalle pd WITH (HOLDLOCK)
            INNER JOIN ControlPresupuestario.CatalogoPartida cp WITH (HOLDLOCK)
                ON cp.IdCatalogoPartida = pd.IdCatalogoPartida
            WHERE pd.IdPresupuestoVersion = @IdPresupuestoVersion AND cp.Activo = 0
        )
            THROW 51212, 'PARTIDA_INACTIVA: no puede formar parte de una nueva línea base.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM ControlPresupuestario.PresupuestoDetalle pd WITH (HOLDLOCK)
            WHERE pd.IdPresupuestoVersion = @IdPresupuestoVersion AND EXISTS
            (
                SELECT 1 FROM ControlPresupuestario.CatalogoPartida hija WITH
                    (HOLDLOCK, INDEX(IX_CatalogoPartida_IdPartidaPadre))
                WHERE hija.IdPartidaPadre = pd.IdCatalogoPartida
            )
        )
            THROW 51213, 'PARTIDA_NO_HOJA: el snapshot contiene una partida con hijos.', 1;

        /*
        Cobertura exclusivamente verificable mediante ledger efectivo.
        Cada componente se acumula con signo en un solo SUM, conservando
        centavos. Un neto negativo tampoco permite ocultar una partida.
        La detección de operaciones pendientes con neto cero queda diferida.
        */
        IF EXISTS
        (
            SELECT pd.IdCatalogoPartida
            FROM ControlPresupuestario.PresupuestoDetalle pd WITH (HOLDLOCK)
            INNER JOIN ControlPresupuestario.PresupuestoVersion pv WITH (HOLDLOCK)
                ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
            INNER JOIN ControlPresupuestario.MovimientoPresupuestal mp WITH (HOLDLOCK)
                ON mp.IdPresupuestoDetalle = pd.IdPresupuestoDetalle
            INNER JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm WITH (HOLDLOCK)
                ON tm.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
            WHERE pv.IdPresupuesto = @IdPresupuesto
                AND pv.IdEstadoPresupuesto IN (@IdAprobado, @IdHistorico)
            GROUP BY pd.IdCatalogoPartida
            HAVING
            (
                SUM(CASE
                    WHEN tm.Codigo = 'COMPROMISO' OR
                        (tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'COMPROMISO' AND mp.Direccion = 'INCREMENTO')
                        THEN CAST(mp.Monto AS DECIMAL(28,2))
                    WHEN tm.Codigo = 'LIBERACION' OR
                        (tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'COMPROMISO' AND mp.Direccion = 'DECREMENTO')
                        THEN -CAST(mp.Monto AS DECIMAL(28,2))
                    ELSE CAST(0 AS DECIMAL(28,2)) END) <> 0
                OR
                SUM(CASE
                    WHEN tm.Codigo = 'EJECUCION' OR
                        (tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'EJECUCION' AND mp.Direccion = 'INCREMENTO')
                        THEN CAST(mp.Monto AS DECIMAL(28,2))
                    WHEN tm.Codigo = 'AJUSTE' AND mp.Afectacion = 'EJECUCION' AND mp.Direccion = 'DECREMENTO'
                        THEN -CAST(mp.Monto AS DECIMAL(28,2))
                    ELSE CAST(0 AS DECIMAL(28,2)) END) <> 0
            )
            AND NOT EXISTS
            (
                SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle objetivo WITH (HOLDLOCK)
                WHERE objetivo.IdPresupuestoVersion = @IdPresupuestoVersion
                    AND objetivo.IdCatalogoPartida = pd.IdCatalogoPartida
            )
        )
            THROW 51214, 'PARTIDA_RELEVANTE_OMITIDA: falta un detalle real para el efecto neto del ledger.', 1;

        DECLARE @FechaAprobacion DATETIME2(0) = SYSUTCDATETIME();

        IF @IdVersionAnterior IS NOT NULL
        BEGIN
            UPDATE ControlPresupuestario.PresupuestoVersion
            SET IdEstadoPresupuesto = @IdHistorico
            WHERE IdPresupuestoVersion = @IdVersionAnterior AND IdEstadoPresupuesto = @IdAprobado;
            IF @@ROWCOUNT <> 1
                THROW 51208, 'VERSIONES_INCONSISTENTES: no se pudo reemplazar la línea base anterior.', 1;
        END;

        UPDATE ControlPresupuestario.PresupuestoVersion
        SET IdEstadoPresupuesto = @IdAprobado,
            FechaAprobacion = @FechaAprobacion,
            UsuarioAprobacion = @UsuarioAprobacion
        WHERE IdPresupuestoVersion = @IdPresupuestoVersion AND IdEstadoPresupuesto = @IdBorrador;
        IF @@ROWCOUNT <> 1
            THROW 51208, 'VERSIONES_INCONSISTENTES: cambió la versión objetivo.', 1;

        IF (SELECT COUNT(*) FROM ControlPresupuestario.PresupuestoVersion WITH (HOLDLOCK)
            WHERE IdPresupuesto = @IdPresupuesto AND IdEstadoPresupuesto = @IdAprobado) <> 1
            THROW 51208, 'VERSIONES_INCONSISTENTES: la aprobación debe dejar una sola línea base vigente.', 1;

        IF @TranCount = 0
            COMMIT TRANSACTION;

        SELECT @IdPresupuesto AS IdPresupuesto,
            @IdPresupuestoVersion AS IdPresupuestoVersionAprobada,
            @NumeroVersion AS NumeroVersion,
            CONVERT(VARCHAR(30), 'APROBADO') AS EstadoPresupuesto,
            @IdVersionAnterior AS IdPresupuestoVersionHistorica,
            @FechaAprobacion AS FechaAprobacion,
            @UsuarioAprobacion AS UsuarioAprobacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_VersionAprobar;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_MovimientoPresupuestal_Registrar
    @IdPresupuestoDetalle INT,
    @TipoMovimiento VARCHAR(30),
    @ClaveEvento VARCHAR(200),
    @Origen VARCHAR(50),
    @IdOrigen INT,
    @Monto DECIMAL(18,2),
    @Fecha DATETIME2(0) = NULL,
    @Observacion NVARCHAR(500) = NULL,
    @Afectacion VARCHAR(20) = NULL,
    @Direccion VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;

    DECLARE @TranCount INT = @@TRANCOUNT;
    DECLARE @SavepointCreado BIT = 0;

    BEGIN TRY
        IF @IdPresupuestoDetalle IS NULL OR @IdPresupuestoDetalle <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuestoDetalle positivo.', 1;
        IF NULLIF(LTRIM(RTRIM(@TipoMovimiento)), '') IS NULL
            THROW 51200, 'CAMPO_REQUERIDO: TipoMovimiento.', 1;
        IF NULLIF(LTRIM(RTRIM(@ClaveEvento)), '') IS NULL
            OR DATALENGTH(@ClaveEvento) <> DATALENGTH(LTRIM(RTRIM(@ClaveEvento)))
            THROW 51200, 'CAMPO_REQUERIDO: ClaveEvento canónica y sin espacios exteriores.', 1;
        IF NULLIF(LTRIM(RTRIM(@Origen)), '') IS NULL
            OR DATALENGTH(@Origen) <> DATALENGTH(LTRIM(RTRIM(@Origen)))
            THROW 51200, 'CAMPO_REQUERIDO: Origen canónico y sin espacios exteriores.', 1;
        IF @IdOrigen IS NULL OR @IdOrigen <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdOrigen positivo.', 1;
        IF @Monto IS NULL
            THROW 51200, 'CAMPO_REQUERIDO: Monto.', 1;
        IF @Monto <= 0
            THROW 51221, 'MONTO_INVALIDO: Monto debe ser positivo.', 1;

        IF DATALENGTH(@TipoMovimiento) <> DATALENGTH(LTRIM(RTRIM(@TipoMovimiento)))
            OR @TipoMovimiento COLLATE Latin1_General_100_BIN2 NOT IN
                ('COMPROMISO', 'LIBERACION', 'EJECUCION', 'AJUSTE')
            THROW 51219, 'TIPO_MOVIMIENTO_INVALIDO: se requiere un código semántico canónico soportado.', 1;

        IF @Afectacion IS NOT NULL AND
            (DATALENGTH(@Afectacion) <> DATALENGTH(LTRIM(RTRIM(@Afectacion)))
             OR @Afectacion COLLATE Latin1_General_100_BIN2 NOT IN ('COMPROMISO', 'EJECUCION'))
            THROW 51220, 'DOMINIO_MOVIMIENTO_INVALIDO: Afectacion.', 1;
        IF @Direccion IS NOT NULL AND
            (DATALENGTH(@Direccion) <> DATALENGTH(LTRIM(RTRIM(@Direccion)))
             OR @Direccion COLLATE Latin1_General_100_BIN2 NOT IN ('INCREMENTO', 'DECREMENTO'))
            THROW 51220, 'DOMINIO_MOVIMIENTO_INVALIDO: Direccion.', 1;

        -- Defensa del contrato, sin CHECK cruzado ni dependencia de IDs fijos.
        IF @TipoMovimiento = 'AJUSTE' AND (@Afectacion IS NULL OR @Direccion IS NULL)
            THROW 51220, 'DOMINIO_MOVIMIENTO_INVALIDO: AJUSTE requiere Afectacion y Direccion.', 1;
        IF @TipoMovimiento IN ('COMPROMISO', 'LIBERACION', 'EJECUCION')
            AND (@Afectacion IS NOT NULL OR @Direccion IS NOT NULL)
            THROW 51220, 'DOMINIO_MOVIMIENTO_INVALIDO: el tipo normal requiere ambas dimensiones en NULL.', 1;

        IF @TranCount = 0
            BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_MovimientoRegistrar;
            SET @SavepointCreado = 1;
        END;

        DECLARE @IdPresupuesto INT;
        SELECT @IdPresupuesto = pv.IdPresupuesto
        FROM ControlPresupuestario.PresupuestoDetalle pd
        INNER JOIN ControlPresupuestario.PresupuestoVersion pv
            ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
        WHERE pd.IdPresupuestoDetalle = @IdPresupuestoDetalle;

        IF @IdPresupuesto IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoDetalle.', 1;

        DECLARE @PresupuestoActivo BIT;
        DECLARE @IdMoneda INT;
        SELECT @PresupuestoActivo = Activo, @IdMoneda = IdMoneda
        FROM ControlPresupuestario.Presupuesto WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Presupuesto))
        WHERE IdPresupuesto = @IdPresupuesto;

        IF @PresupuestoActivo IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: Presupuesto.', 1;

        DECLARE @IdPresupuestoVersion INT;
        DECLARE @EstadoPresupuesto VARCHAR(30);
        SELECT @IdPresupuestoVersion = pv.IdPresupuestoVersion, @EstadoPresupuesto = ep.Codigo
        FROM ControlPresupuestario.PresupuestoDetalle pd WITH (HOLDLOCK)
        INNER JOIN ControlPresupuestario.PresupuestoVersion pv WITH (HOLDLOCK)
            ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
        INNER JOIN ControlPresupuestario.EstadoPresupuesto ep WITH (HOLDLOCK)
            ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
        WHERE pd.IdPresupuestoDetalle = @IdPresupuestoDetalle AND pv.IdPresupuesto = @IdPresupuesto;

        IF @IdPresupuestoVersion IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoDetalle original.', 1;

        DECLARE @CodigoMoneda VARCHAR(3);
        SELECT @CodigoMoneda = Codigo
        FROM ControlPresupuestario.Moneda WITH (HOLDLOCK)
        WHERE IdMoneda = @IdMoneda;

        IF @CodigoMoneda IS NULL
            THROW 51204, 'CATALOGO_ESTRUCTURAL_INVALIDO: Moneda del presupuesto.', 1;

        DECLARE @IdTipoMovimiento INT;
        DECLARE @TipoActivo BIT;
        SELECT @IdTipoMovimiento = IdTipoMovimientoPresupuestal, @TipoActivo = Activo
        FROM ControlPresupuestario.TipoMovimientoPresupuestal WITH (HOLDLOCK)
        WHERE Codigo = @TipoMovimiento;

        IF @IdTipoMovimiento IS NULL
            THROW 51219, 'TIPO_MOVIMIENTO_INVALIDO: el código no existe en el catálogo.', 1;

        DECLARE @IdMovimiento BIGINT;
        DECLARE @DetalleExistente INT;
        DECLARE @TipoExistente INT;
        DECLARE @OrigenExistente VARCHAR(50);
        DECLARE @IdOrigenExistente INT;
        DECLARE @MontoExistente DECIMAL(18,2);
        DECLARE @FechaExistente DATETIME2(0);
        DECLARE @ObservacionExistente NVARCHAR(500);
        DECLARE @AfectacionExistente VARCHAR(20);
        DECLARE @DireccionExistente VARCHAR(20);
        DECLARE @EsNuevo BIT = 0;

        -- La clave es global, no se filtra por detalle ni por presupuesto.
        SELECT @IdMovimiento = IdMovimientoPresupuestal,
            @DetalleExistente = IdPresupuestoDetalle,
            @TipoExistente = IdTipoMovimientoPresupuestal,
            @OrigenExistente = Origen,
            @IdOrigenExistente = IdOrigen,
            @MontoExistente = Monto,
            @FechaExistente = Fecha,
            @ObservacionExistente = Observacion,
            @AfectacionExistente = Afectacion,
            @DireccionExistente = Direccion
        FROM ControlPresupuestario.MovimientoPresupuestal WITH
            (UPDLOCK, HOLDLOCK, INDEX(UQ_MovimientoPresupuestal_ClaveEvento))
        WHERE ClaveEvento = @ClaveEvento;

        IF @IdMovimiento IS NOT NULL
        BEGIN
            IF @DetalleExistente <> @IdPresupuestoDetalle
                OR @TipoExistente <> @IdTipoMovimiento
                OR @IdOrigenExistente <> @IdOrigen
                OR @MontoExistente <> @Monto
                OR (@Fecha IS NOT NULL AND @FechaExistente <> @Fecha)
                OR DATALENGTH(@OrigenExistente) <> DATALENGTH(@Origen)
                OR CONVERT(VARBINARY(MAX), @OrigenExistente) <> CONVERT(VARBINARY(MAX), @Origen)
                OR (@ObservacionExistente IS NULL AND @Observacion IS NOT NULL)
                OR (@ObservacionExistente IS NOT NULL AND @Observacion IS NULL)
                OR (@ObservacionExistente IS NOT NULL AND @Observacion IS NOT NULL AND
                    (DATALENGTH(@ObservacionExistente) <> DATALENGTH(@Observacion)
                     OR CONVERT(VARBINARY(MAX), @ObservacionExistente) <> CONVERT(VARBINARY(MAX), @Observacion)))
                OR (@AfectacionExistente IS NULL AND @Afectacion IS NOT NULL)
                OR (@AfectacionExistente IS NOT NULL AND @Afectacion IS NULL)
                OR (@AfectacionExistente IS NOT NULL AND @Afectacion IS NOT NULL AND
                    (DATALENGTH(@AfectacionExistente) <> DATALENGTH(@Afectacion)
                     OR CONVERT(VARBINARY(MAX), @AfectacionExistente) <> CONVERT(VARBINARY(MAX), @Afectacion)))
                OR (@DireccionExistente IS NULL AND @Direccion IS NOT NULL)
                OR (@DireccionExistente IS NOT NULL AND @Direccion IS NULL)
                OR (@DireccionExistente IS NOT NULL AND @Direccion IS NOT NULL AND
                    (DATALENGTH(@DireccionExistente) <> DATALENGTH(@Direccion)
                     OR CONVERT(VARBINARY(MAX), @DireccionExistente) <> CONVERT(VARBINARY(MAX), @Direccion)))
                THROW 51230, 'CONFLICTO_IDEMPOTENCIA: ClaveEvento ya representa un contenido diferente.', 1;
        END;
        ELSE
        BEGIN
            IF @PresupuestoActivo = 0
                THROW 51202, 'RECURSO_INACTIVO: Presupuesto.', 1;
            IF @TipoActivo = 0
                THROW 51202, 'RECURSO_INACTIVO: TipoMovimientoPresupuestal.', 1;
            IF @EstadoPresupuesto NOT IN ('APROBADO', 'HISTORICO')
                THROW 51207, 'ESTADO_INVALIDO: el detalle debe pertenecer a APROBADO/HISTORICO.', 1;

            SET @Fecha = COALESCE(@Fecha, CONVERT(DATETIME2(0), SYSDATETIME()));
            INSERT INTO ControlPresupuestario.MovimientoPresupuestal
                (IdPresupuestoDetalle, IdTipoMovimientoPresupuestal, ClaveEvento,
                 Origen, IdOrigen, Monto, Fecha, Observacion, Afectacion, Direccion)
            VALUES
                (@IdPresupuestoDetalle, @IdTipoMovimiento, @ClaveEvento,
                 @Origen, @IdOrigen, @Monto, @Fecha, @Observacion, @Afectacion, @Direccion);
            SET @IdMovimiento = CONVERT(BIGINT, SCOPE_IDENTITY());
            SET @EsNuevo = 1;
        END;

        -- Capturar la fila persistida antes de COMMIT; no regenerar Fecha en retry.
        DECLARE @Resultado TABLE
        (
            IdMovimientoPresupuestal BIGINT,
            IdPresupuestoDetalle INT,
            IdTipoMovimientoPresupuestal INT,
            TipoMovimiento VARCHAR(30),
            ClaveEvento VARCHAR(200),
            Origen VARCHAR(50),
            IdOrigen INT,
            Afectacion VARCHAR(20) NULL,
            Direccion VARCHAR(20) NULL,
            Fecha DATETIME2(0),
            Monto DECIMAL(18,2),
            Observacion NVARCHAR(500) NULL,
            IdPresupuesto INT,
            IdPresupuestoVersion INT,
            IdMoneda INT,
            CodigoMoneda VARCHAR(3),
            EsNuevo BIT
        );
        INSERT INTO @Resultado
        SELECT mp.IdMovimientoPresupuestal, mp.IdPresupuestoDetalle,
            mp.IdTipoMovimientoPresupuestal, @TipoMovimiento, mp.ClaveEvento,
            mp.Origen, mp.IdOrigen, mp.Afectacion, mp.Direccion, mp.Fecha,
            mp.Monto, mp.Observacion, @IdPresupuesto, @IdPresupuestoVersion,
            @IdMoneda, @CodigoMoneda, @EsNuevo
        FROM ControlPresupuestario.MovimientoPresupuestal mp WITH (HOLDLOCK)
        WHERE mp.IdMovimientoPresupuestal = @IdMovimiento;

        IF @TranCount = 0
            COMMIT TRANSACTION;

        SELECT IdMovimientoPresupuestal, IdPresupuestoDetalle,
            IdTipoMovimientoPresupuestal, TipoMovimiento, ClaveEvento, Origen,
            IdOrigen, Afectacion, Direccion, Fecha, Monto, Observacion,
            IdPresupuesto, IdPresupuestoVersion, IdMoneda, CodigoMoneda, EsNuevo
        FROM @Resultado;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_MovimientoRegistrar;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_PresupuestoVersion_CrearNueva
    @IdPresupuesto INT,
    @Descripcion NVARCHAR(500) = NULL,
    @MotivoCambio NVARCHAR(500) = NULL,
    @UsuarioCreacion NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;

    DECLARE @TranCount INT = @@TRANCOUNT;
    DECLARE @SavepointCreado BIT = 0;

    BEGIN TRY
        IF @IdPresupuesto IS NULL OR @IdPresupuesto <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuesto positivo.', 1;

        IF @TranCount = 0
            BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_VersionCrearNueva;
            SET @SavepointCreado = 1;
        END;

        DECLARE @PresupuestoActivo BIT;
        DECLARE @IdMoneda INT;
        SELECT @PresupuestoActivo = Activo, @IdMoneda = IdMoneda
        FROM ControlPresupuestario.Presupuesto WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Presupuesto))
        WHERE IdPresupuesto = @IdPresupuesto;

        IF @PresupuestoActivo IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: Presupuesto.', 1;
        IF @PresupuestoActivo = 0
            THROW 51202, 'RECURSO_INACTIVO: Presupuesto.', 1;

        DECLARE @MonedaActiva BIT;
        SELECT @MonedaActiva = Activo
        FROM ControlPresupuestario.Moneda WITH (HOLDLOCK)
        WHERE IdMoneda = @IdMoneda;

        IF @MonedaActiva IS NULL
            THROW 51204, 'CATALOGO_ESTRUCTURAL_INVALIDO: Moneda del presupuesto.', 1;
        IF @MonedaActiva = 0
            THROW 51202, 'RECURSO_INACTIVO: Moneda.', 1;

        DECLARE @IdBorrador INT;
        DECLARE @IdAprobado INT;
        SELECT
            @IdBorrador = MAX(CASE WHEN Codigo = 'BORRADOR' THEN IdEstadoPresupuesto END),
            @IdAprobado = MAX(CASE WHEN Codigo = 'APROBADO' THEN IdEstadoPresupuesto END)
        FROM ControlPresupuestario.EstadoPresupuesto WITH (HOLDLOCK)
        WHERE Codigo IN ('BORRADOR', 'APROBADO') AND Activo = 1;

        IF @IdBorrador IS NULL OR @IdAprobado IS NULL
            THROW 51204, 'CATALOGO_ESTRUCTURAL_INVALIDO: BORRADOR/APROBADO no disponibles.', 1;

        DECLARE @CantidadAprobadas INT;
        DECLARE @CantidadBorradores INT;
        DECLARE @NumeroAnterior INT;
        DECLARE @IdVersionBase INT;
        SELECT
            @CantidadAprobadas = COUNT(CASE WHEN IdEstadoPresupuesto = @IdAprobado THEN 1 END),
            @CantidadBorradores = COUNT(CASE WHEN IdEstadoPresupuesto = @IdBorrador THEN 1 END),
            @NumeroAnterior = MAX(NumeroVersion),
            @IdVersionBase = MAX(CASE WHEN IdEstadoPresupuesto = @IdAprobado
                THEN IdPresupuestoVersion END)
        FROM ControlPresupuestario.PresupuestoVersion WITH (UPDLOCK, HOLDLOCK)
        WHERE IdPresupuesto = @IdPresupuesto;

        IF @CantidadAprobadas > 1 OR @CantidadBorradores > 1
            THROW 51208, 'VERSIONES_INCONSISTENTES: múltiples APROBADO/BORRADOR.', 1;
        IF @CantidadAprobadas = 0
            THROW 51210, 'SIN_VERSION_APROBADA: no existe una línea base para copiar.', 1;
        IF @CantidadBorradores = 1
            THROW 51211, 'BORRADOR_PENDIENTE: ya existe una revisión en preparación.', 1;
        IF @NumeroAnterior = 2147483647
            THROW 51216, 'NUMERACION_AGOTADA: no es posible crear otra versión INT.', 1;

        DECLARE @CantidadDetalles INT;
        SELECT @CantidadDetalles = COUNT(*)
        FROM ControlPresupuestario.PresupuestoDetalle WITH (HOLDLOCK)
        WHERE IdPresupuestoVersion = @IdVersionBase;

        IF @CantidadDetalles = 0 OR EXISTS
        (
            SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle WITH (HOLDLOCK)
            WHERE IdPresupuestoVersion = @IdVersionBase AND MontoPresupuestado < 0
        )
            THROW 51209, 'SNAPSHOT_INVALIDO: la línea base debe tener detalles válidos.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM ControlPresupuestario.PresupuestoDetalle pd WITH (HOLDLOCK)
            INNER JOIN ControlPresupuestario.CatalogoPartida cp WITH (HOLDLOCK)
                ON cp.IdCatalogoPartida = pd.IdCatalogoPartida
            WHERE pd.IdPresupuestoVersion = @IdVersionBase AND cp.Activo = 0
        )
            THROW 51212, 'PARTIDA_INACTIVA: no puede copiarse a una nueva versión.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM ControlPresupuestario.PresupuestoDetalle pd WITH (HOLDLOCK)
            WHERE pd.IdPresupuestoVersion = @IdVersionBase AND EXISTS
            (
                SELECT 1 FROM ControlPresupuestario.CatalogoPartida hija WITH
                    (HOLDLOCK, INDEX(IX_CatalogoPartida_IdPartidaPadre))
                WHERE hija.IdPartidaPadre = pd.IdCatalogoPartida
            )
        )
            THROW 51213, 'PARTIDA_NO_HOJA: la línea base contiene una partida con hijos.', 1;

        DECLARE @NumeroVersion INT = @NumeroAnterior + 1;
        DECLARE @FechaCreacion DATETIME2(0) = SYSUTCDATETIME();
        DECLARE @IdPresupuestoVersion INT;
        DECLARE @CantidadDetallesCopiados INT;

        INSERT INTO ControlPresupuestario.PresupuestoVersion
            (IdPresupuesto, IdEstadoPresupuesto, NumeroVersion, Descripcion,
             MotivoCambio, FechaCreacion, UsuarioCreacion)
        VALUES
            (@IdPresupuesto, @IdBorrador, @NumeroVersion, @Descripcion,
             @MotivoCambio, @FechaCreacion, @UsuarioCreacion);
        SET @IdPresupuestoVersion = CONVERT(INT, SCOPE_IDENTITY());

        INSERT INTO ControlPresupuestario.PresupuestoDetalle
            (IdPresupuestoVersion, IdCatalogoPartida, MontoPresupuestado, Observacion)
        SELECT @IdPresupuestoVersion, IdCatalogoPartida, MontoPresupuestado, Observacion
        FROM ControlPresupuestario.PresupuestoDetalle WITH (HOLDLOCK)
        WHERE IdPresupuestoVersion = @IdVersionBase;
        SET @CantidadDetallesCopiados = @@ROWCOUNT;

        IF @CantidadDetallesCopiados <> @CantidadDetalles
            THROW 51209, 'SNAPSHOT_INVALIDO: la copia no coincide con el snapshot base.', 1;

        IF @TranCount = 0
            COMMIT TRANSACTION;

        SELECT @IdPresupuesto AS IdPresupuesto,
            @IdPresupuestoVersion AS IdPresupuestoVersion,
            @NumeroVersion AS NumeroVersion,
            CONVERT(VARCHAR(30), 'BORRADOR') AS EstadoPresupuesto,
            @IdVersionBase AS IdPresupuestoVersionBase,
            @CantidadDetallesCopiados AS CantidadDetallesCopiados,
            @FechaCreacion AS FechaCreacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_VersionCrearNueva;
        THROW;
    END CATCH;
END;
GO
