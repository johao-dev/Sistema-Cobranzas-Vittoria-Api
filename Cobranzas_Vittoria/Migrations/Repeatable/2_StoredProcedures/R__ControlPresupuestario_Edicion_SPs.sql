/* Operaciones secundarias de snapshots y cabecera.
Protocolo: localizar -> PK Presupuesto UPDLOCK/HOLDLOCK -> releer versión/detalle.
Resultados capturados antes de COMMIT.
*/

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_PresupuestoDetalle_Agregar
    @IdPresupuestoVersion INT,
    @IdCatalogoPartida INT,
    @MontoPresupuestado DECIMAL(18,2),
    @Observacion NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;

    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @IdPresupuestoVersion IS NULL OR @IdPresupuestoVersion <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuestoVersion positivo.', 1;
        IF @IdCatalogoPartida IS NULL OR @IdCatalogoPartida <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdCatalogoPartida positivo.', 1;
        IF @MontoPresupuestado IS NULL
            THROW 51200, 'CAMPO_REQUERIDO: MontoPresupuestado.', 1;
        IF @MontoPresupuestado < 0
            THROW 51221, 'MONTO_INVALIDO: el presupuesto admite cero, no negativos.', 1;
        
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_PresupuestoDetalleAgregar;
            SET @SavepointCreado = 1;
        END;

        DECLARE @IdPresupuesto INT;
        SELECT @IdPresupuesto = IdPresupuesto
        FROM ControlPresupuestario.PresupuestoVersion
        WHERE IdPresupuestoVersion = @IdPresupuestoVersion;

        IF @IdPresupuesto IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoVersion.', 1;

        DECLARE @PresupuestoActivo BIT;
        SELECT @PresupuestoActivo = Activo
        FROM ControlPresupuestario.Presupuesto WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Presupuesto))
        WHERE IdPresupuesto = @IdPresupuesto;

        IF @PresupuestoActivo IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: Presupuesto.', 1;
        IF @PresupuestoActivo = 0
            THROW 51202, 'RECURSO_INACTIVO: Presupuesto.', 1;

        DECLARE @Estado VARCHAR(30);
        SELECT @Estado = e.Codigo
        FROM ControlPresupuestario.PresupuestoVersion v WITH (UPDLOCK, HOLDLOCK)
        JOIN ControlPresupuestario.EstadoPresupuesto e WITH (HOLDLOCK)
            ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
        WHERE v.IdPresupuestoVersion = @IdPresupuestoVersion AND v.IdPresupuesto = @IdPresupuesto;

        IF @Estado IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoVersion.', 1;

        IF @Estado <> 'BORRADOR'
            THROW 51207, 'ESTADO_INVALIDO: solo se permite BORRADOR.', 1;

        DECLARE @PartidaActiva BIT;
        SELECT @PartidaActiva = Activo
        FROM ControlPresupuestario.CatalogoPartida WITH (HOLDLOCK)
        WHERE IdCatalogoPartida = @IdCatalogoPartida;

        IF @PartidaActiva IS NULL
            THROW 51201, 'REFERENCIA_NO_EXISTE: CatalogoPartida.', 1;
        IF @PartidaActiva = 0
            THROW 51212, 'PARTIDA_INACTIVA: no puede agregarse al snapshot.', 1;

        IF EXISTS (SELECT 1 FROM ControlPresupuestario.CatalogoPartida WITH
            (HOLDLOCK, INDEX(IX_CatalogoPartida_IdPartidaPadre)) WHERE IdPartidaPadre = @IdCatalogoPartida)
            THROW 51213, 'PARTIDA_NO_HOJA: tiene hijos, incluidos los inactivos.', 1;
        IF EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle WITH (UPDLOCK, HOLDLOCK)
            WHERE IdPresupuestoVersion = @IdPresupuestoVersion AND IdCatalogoPartida = @IdCatalogoPartida)
            THROW 51215, 'PARTIDA_DUPLICADA: ya existe en esta versión.', 1;

        DECLARE @FechaCreacion DATETIME2(0) = SYSDATETIME(), @IdPresupuestoDetalle INT;
        INSERT INTO ControlPresupuestario.PresupuestoDetalle
            (IdPresupuestoVersion, IdCatalogoPartida, MontoPresupuestado, Observacion, FechaCreacion)
        VALUES (@IdPresupuestoVersion, @IdCatalogoPartida, @MontoPresupuestado, @Observacion, @FechaCreacion);
        SET @IdPresupuestoDetalle = CONVERT(INT, SCOPE_IDENTITY());

        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdPresupuestoDetalle AS IdPresupuestoDetalle, @IdPresupuestoVersion AS IdPresupuestoVersion,
            @IdCatalogoPartida AS IdCatalogoPartida, @MontoPresupuestado AS MontoPresupuestado,
            @Observacion AS Observacion, @FechaCreacion AS FechaCreacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_PresupuestoDetalleAgregar;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_PresupuestoDetalle_Actualizar
    @IdPresupuestoDetalle INT,
    @MontoPresupuestado DECIMAL(18,2),
    @Observacion NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @IdPresupuestoDetalle IS NULL OR @IdPresupuestoDetalle <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuestoDetalle positivo.', 1;
        IF @MontoPresupuestado IS NULL THROW 51200, 'CAMPO_REQUERIDO: MontoPresupuestado.', 1;
        IF @MontoPresupuestado < 0 THROW 51221, 'MONTO_INVALIDO: el presupuesto admite cero, no negativos.', 1;
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_PresupuestoDetalleActualizar;
            SET @SavepointCreado = 1;
        END;
        DECLARE @IdPresupuesto INT, @IdPresupuestoVersion INT;
        SELECT @IdPresupuesto = v.IdPresupuesto, @IdPresupuestoVersion = v.IdPresupuestoVersion
        FROM ControlPresupuestario.PresupuestoDetalle d
        JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuestoVersion = d.IdPresupuestoVersion
        WHERE d.IdPresupuestoDetalle = @IdPresupuestoDetalle;
        IF @IdPresupuesto IS NULL THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoDetalle.', 1;
        DECLARE @PresupuestoActivo BIT;
        SELECT @PresupuestoActivo = Activo
        FROM ControlPresupuestario.Presupuesto WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Presupuesto))
        WHERE IdPresupuesto = @IdPresupuesto;
        IF @PresupuestoActivo IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: Presupuesto.', 1;
        IF @PresupuestoActivo = 0
            THROW 51202, 'RECURSO_INACTIVO: Presupuesto.', 1;
        DECLARE @Estado VARCHAR(30);
        SELECT @Estado = e.Codigo
        FROM ControlPresupuestario.PresupuestoVersion v WITH (UPDLOCK, HOLDLOCK)
        JOIN ControlPresupuestario.EstadoPresupuesto e WITH (HOLDLOCK)
            ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
        WHERE v.IdPresupuestoVersion = @IdPresupuestoVersion AND v.IdPresupuesto = @IdPresupuesto;
        IF @Estado IS NULL THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoVersion.', 1;
        IF @Estado <> 'BORRADOR' THROW 51207, 'ESTADO_INVALIDO: solo se permite BORRADOR.', 1;
        IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle WITH (UPDLOCK, HOLDLOCK)
            WHERE IdPresupuestoDetalle = @IdPresupuestoDetalle AND IdPresupuestoVersion = @IdPresupuestoVersion)
            THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoDetalle original.', 1;
        DECLARE @FechaModificacion DATETIME2(0) = SYSDATETIME();
        UPDATE ControlPresupuestario.PresupuestoDetalle
        SET MontoPresupuestado = @MontoPresupuestado, Observacion = @Observacion,
            FechaModificacion = @FechaModificacion
        WHERE IdPresupuestoDetalle = @IdPresupuestoDetalle;
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdPresupuestoDetalle AS IdPresupuestoDetalle, @IdPresupuestoVersion AS IdPresupuestoVersion,
            @MontoPresupuestado AS MontoPresupuestado, @Observacion AS Observacion,
            @FechaModificacion AS FechaModificacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_PresupuestoDetalleActualizar;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_PresupuestoDetalle_Eliminar
    @IdPresupuestoDetalle INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @IdPresupuestoDetalle IS NULL OR @IdPresupuestoDetalle <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuestoDetalle positivo.', 1;
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_PresupuestoDetalleEliminar;
            SET @SavepointCreado = 1;
        END;
        DECLARE @IdPresupuesto INT, @IdPresupuestoVersion INT;
        SELECT @IdPresupuesto = v.IdPresupuesto, @IdPresupuestoVersion = v.IdPresupuestoVersion
        FROM ControlPresupuestario.PresupuestoDetalle d
        JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuestoVersion = d.IdPresupuestoVersion
        WHERE d.IdPresupuestoDetalle = @IdPresupuestoDetalle;
        IF @IdPresupuesto IS NULL THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoDetalle.', 1;
        DECLARE @PresupuestoActivo BIT;
        SELECT @PresupuestoActivo = Activo
        FROM ControlPresupuestario.Presupuesto WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Presupuesto))
        WHERE IdPresupuesto = @IdPresupuesto;
        IF @PresupuestoActivo IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: Presupuesto.', 1;
        IF @PresupuestoActivo = 0
            THROW 51202, 'RECURSO_INACTIVO: Presupuesto.', 1;
        DECLARE @Estado VARCHAR(30);
        SELECT @Estado = e.Codigo
        FROM ControlPresupuestario.PresupuestoVersion v WITH (UPDLOCK, HOLDLOCK)
        JOIN ControlPresupuestario.EstadoPresupuesto e WITH (HOLDLOCK)
            ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
        WHERE v.IdPresupuestoVersion = @IdPresupuestoVersion AND v.IdPresupuesto = @IdPresupuesto;
        IF @Estado IS NULL THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoVersion.', 1;
        IF @Estado <> 'BORRADOR' THROW 51207, 'ESTADO_INVALIDO: solo se permite BORRADOR.', 1;
        IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle WITH (UPDLOCK, HOLDLOCK)
            WHERE IdPresupuestoDetalle = @IdPresupuestoDetalle AND IdPresupuestoVersion = @IdPresupuestoVersion)
            THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoDetalle original.', 1;
        -- FK 547 se conserva si existen referencias ajenas al protocolo; no borrar dependientes.
        DELETE FROM ControlPresupuestario.PresupuestoDetalle WHERE IdPresupuestoDetalle = @IdPresupuestoDetalle;
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdPresupuestoDetalle AS IdPresupuestoDetalle, @IdPresupuestoVersion AS IdPresupuestoVersion,
            CONVERT(BIT, 1) AS Eliminado;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_PresupuestoDetalleEliminar;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_PresupuestoVersion_Anular
    @IdPresupuestoVersion INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @IdPresupuestoVersion IS NULL OR @IdPresupuestoVersion <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuestoVersion positivo.', 1;
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_PresupuestoVersionAnular;
            SET @SavepointCreado = 1;
        END;
        DECLARE @IdPresupuesto INT;
        SELECT @IdPresupuesto = IdPresupuesto FROM ControlPresupuestario.PresupuestoVersion
        WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
        IF @IdPresupuesto IS NULL THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoVersion.', 1;
        DECLARE @PresupuestoActivo BIT;
        SELECT @PresupuestoActivo = Activo
        FROM ControlPresupuestario.Presupuesto WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Presupuesto))
        WHERE IdPresupuesto = @IdPresupuesto;
        IF @PresupuestoActivo IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: Presupuesto.', 1;
        IF @PresupuestoActivo = 0
            THROW 51202, 'RECURSO_INACTIVO: Presupuesto.', 1;
        DECLARE @Estado VARCHAR(30);
        SELECT @Estado = e.Codigo
        FROM ControlPresupuestario.PresupuestoVersion v WITH (UPDLOCK, HOLDLOCK)
        JOIN ControlPresupuestario.EstadoPresupuesto e WITH (HOLDLOCK)
            ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
        WHERE v.IdPresupuestoVersion = @IdPresupuestoVersion AND v.IdPresupuesto = @IdPresupuesto;
        IF @Estado IS NULL THROW 51203, 'RECURSO_NO_EXISTE: PresupuestoVersion.', 1;
        IF @Estado <> 'BORRADOR' THROW 51207, 'ESTADO_INVALIDO: solo se permite BORRADOR.', 1;
        DECLARE @IdAnulado INT, @NumeroVersion INT;
        SELECT @IdAnulado = IdEstadoPresupuesto FROM ControlPresupuestario.EstadoPresupuesto WITH (HOLDLOCK)
        WHERE Codigo = 'ANULADO' AND Activo = 1;
        IF @IdAnulado IS NULL THROW 51204, 'CATALOGO_ESTRUCTURAL_INVALIDO: ANULADO no disponible.', 1;
        SELECT @NumeroVersion = NumeroVersion FROM ControlPresupuestario.PresupuestoVersion
        WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
        UPDATE ControlPresupuestario.PresupuestoVersion SET IdEstadoPresupuesto = @IdAnulado
        WHERE IdPresupuestoVersion = @IdPresupuestoVersion;
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdPresupuesto AS IdPresupuesto, @IdPresupuestoVersion AS IdPresupuestoVersion,
            @NumeroVersion AS NumeroVersion, CONVERT(VARCHAR(30), 'ANULADO') AS EstadoPresupuesto;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_PresupuestoVersionAnular;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_Presupuesto_Actualizar
    @IdPresupuesto INT,
    @Nombre NVARCHAR(200),
    @Activo BIT,
    @Descripcion NVARCHAR(500) = NULL,
    @FechaInicio DATE = NULL,
    @FechaFin DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @IdPresupuesto IS NULL OR @IdPresupuesto <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuesto positivo.', 1;
        SET @Nombre = LTRIM(RTRIM(@Nombre));
        IF NULLIF(@Nombre, N'') IS NULL OR @Activo IS NULL
            THROW 51200, 'CAMPO_REQUERIDO: Nombre y Activo.', 1;
        IF @FechaInicio IS NOT NULL AND @FechaFin IS NOT NULL AND @FechaFin < @FechaInicio
            THROW 51206, 'RANGO_FECHAS_INVALIDO: FechaFin es anterior a FechaInicio.', 1;
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_PresupuestoActualizar;
            SET @SavepointCreado = 1;
        END;
        DECLARE @PresupuestoActivo BIT;
        SELECT @PresupuestoActivo = Activo
        FROM ControlPresupuestario.Presupuesto WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Presupuesto))
        WHERE IdPresupuesto = @IdPresupuesto;
        IF @PresupuestoActivo IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: Presupuesto.', 1;
        DECLARE @FechaModificacion DATETIME2(0) = SYSUTCDATETIME();
        -- Codigo, IdCentroCosto e IdMoneda son estables durante toda su vida.
        UPDATE ControlPresupuestario.Presupuesto
        SET Nombre = @Nombre, Descripcion = @Descripcion, FechaInicio = @FechaInicio,
            FechaFin = @FechaFin, Activo = @Activo, FechaModificacion = @FechaModificacion
        WHERE IdPresupuesto = @IdPresupuesto;
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdPresupuesto AS IdPresupuesto, @Nombre AS Nombre, @Descripcion AS Descripcion,
            @FechaInicio AS FechaInicio, @FechaFin AS FechaFin, @Activo AS Activo,
            @FechaModificacion AS FechaModificacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_PresupuestoActualizar;
        THROW;
    END CATCH;
END;
GO


