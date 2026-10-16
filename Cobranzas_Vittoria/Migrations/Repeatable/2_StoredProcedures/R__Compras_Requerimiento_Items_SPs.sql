-- Gestión de ítems en almacén. Los SP repiten solo las invariantes que deben verificarse al escribir.
-- 51460: entrada; 51461/51464: recurso ausente; 51462–51469: conflicto.
CREATE OR ALTER PROCEDURE compras.usp_Requerimiento_ObtenerConItems
    @IdRequerimiento INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @TranCount INT = @@TRANCOUNT;
    IF XACT_STATE() = -1 THROW 51469, 'TRANSACCION_NO_CONFIRMABLE: se requiere rollback externo.', 1;
    BEGIN TRY
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE SAVE TRANSACTION ReqObtenerItems;
        -- Mismo mutex que los escritores: cabecera e ítems corresponden al mismo instante lógico.
        SELECT IdRequerimiento, NumeroRequerimiento, FechaRequerimiento, IdProyecto, Descripcion,
            FechaEntrega, IdUsuarioSolicitante, Estado, Observacion, FechaCreacion
        FROM compras.Requerimiento WITH (UPDLOCK, HOLDLOCK)
        WHERE IdRequerimiento = @IdRequerimiento;
        SELECT IdRequerimientoDetalle, IdRequerimiento, IdMaterial, Cantidad, Observacion, IdPresupuestoDetalle
        FROM compras.RequerimientoDetalle
        WHERE IdRequerimiento = @IdRequerimiento ORDER BY IdRequerimientoDetalle;
        IF @TranCount = 0 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND XACT_STATE() = 1 ROLLBACK TRANSACTION ReqObtenerItems;
        THROW;
    END CATCH;
END;
GO

-- Validación compartida para no mantener tres versiones de las reglas de estado y referencias.
-- Debe llamarse dentro de la transacción de cada operación; el bloqueo se mantiene hasta su final.
CREATE OR ALTER PROCEDURE compras.usp_RequerimientoItem_ValidarGestion
    @IdRequerimiento INT,
    @IdMaterial INT = NULL,
    @IdPresupuestoDetalle INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT = 0 THROW 51469, 'TRANSACCION_REQUERIDA: la validación necesita una transacción.', 1;
    IF @IdRequerimiento IS NULL OR @IdRequerimiento <= 0
        THROW 51460, 'IDENTIFICADOR_INVALIDO: IdRequerimiento debe ser positivo.', 1;
    DECLARE @Estado NVARCHAR(30), @IdProyecto INT;
    SELECT @Estado = Estado, @IdProyecto = IdProyecto
    FROM compras.Requerimiento WITH (UPDLOCK, HOLDLOCK) WHERE IdRequerimiento = @IdRequerimiento;
    IF @Estado IS NULL THROW 51461, 'REQUERIMIENTO_NO_ENCONTRADO: el requerimiento no existe.', 1;
    IF @Estado <> N'EnviadoAlmacen'
        THROW 51462, 'ESTADO_REQUERIMIENTO_INVALIDO: solo se gestionan ítems en estado EnviadoAlmacen.', 1;
    IF @IdMaterial IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM maestra.Material WITH (HOLDLOCK) WHERE IdMaterial = @IdMaterial AND Activo = 1)
        THROW 51466, 'REQUERIMIENTO_MATERIAL_INVALIDO: el material no existe o está inactivo.', 1;
    IF @IdPresupuestoDetalle IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle pd WITH (HOLDLOCK)
        JOIN ControlPresupuestario.PresupuestoVersion pv WITH (HOLDLOCK)
            ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
        JOIN ControlPresupuestario.EstadoPresupuesto ep WITH (HOLDLOCK)
            ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
        JOIN ControlPresupuestario.Presupuesto p WITH (HOLDLOCK) ON p.IdPresupuesto = pv.IdPresupuesto
        JOIN ControlPresupuestario.CentroCosto cc WITH (HOLDLOCK) ON cc.IdCentroCosto = p.IdCentroCosto
        WHERE pd.IdPresupuestoDetalle = @IdPresupuestoDetalle AND ep.Codigo = 'APROBADO'
            AND p.Activo = 1 AND cc.Activo = 1 AND cc.IdProyecto = @IdProyecto)
        THROW 51467, 'PARTIDA_REQUERIMIENTO_INVALIDA: la partida debe estar vigente y pertenecer al proyecto.', 1;
END;
GO

CREATE OR ALTER PROCEDURE compras.usp_RequerimientoItem_AgregarAlmacen
    @IdRequerimiento INT,
    @IdMaterial INT,
    @Cantidad DECIMAL(38,18),
    @IdPresupuestoDetalle INT = NULL,
    @Observacion NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @IdMaterial IS NULL OR @IdMaterial <= 0 OR @IdPresupuestoDetalle <= 0
        THROW 51460, 'IDENTIFICADOR_INVALIDO: los identificadores informados deben ser positivos.', 1;
    IF @Cantidad IS NULL OR @Cantidad <= 0 OR @Cantidad > 9999999999999999.99 OR @Cantidad <> ROUND(@Cantidad, 2)
        THROW 51460, 'REQUERIMIENTO_CANTIDAD_INVALIDA: cantidad positiva DECIMAL(18,2), sin redondear.', 1;
    IF DATALENGTH(@Observacion) > 500
        THROW 51460, 'LONGITUD_EXCEDIDA: Observacion admite hasta 250 caracteres.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT;
    IF XACT_STATE() = -1 THROW 51469, 'TRANSACCION_NO_CONFIRMABLE: se requiere rollback externo.', 1;
    BEGIN TRY
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE SAVE TRANSACTION ReqAgregarItem;
        EXEC compras.usp_RequerimientoItem_ValidarGestion @IdRequerimiento, @IdMaterial, @IdPresupuestoDetalle;
        IF EXISTS (SELECT 1 FROM compras.RequerimientoDetalle
            WHERE IdRequerimiento = @IdRequerimiento AND IdMaterial = @IdMaterial)
            THROW 51463, 'MATERIAL_DUPLICADO: el material ya existe en el requerimiento.', 1;
        INSERT INTO compras.RequerimientoDetalle (IdRequerimiento, IdMaterial, Cantidad, Observacion, IdPresupuestoDetalle)
        VALUES (@IdRequerimiento, @IdMaterial, @Cantidad, @Observacion, @IdPresupuestoDetalle);
        DECLARE @IdRequerimientoDetalle INT = CONVERT(INT, SCOPE_IDENTITY());
        SELECT IdRequerimientoDetalle, IdRequerimiento, IdMaterial, Cantidad, Observacion, IdPresupuestoDetalle
        FROM compras.RequerimientoDetalle
        WHERE IdRequerimiento = @IdRequerimiento AND IdRequerimientoDetalle = @IdRequerimientoDetalle;
        IF @TranCount = 0 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND XACT_STATE() = 1 ROLLBACK TRANSACTION ReqAgregarItem;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE compras.usp_RequerimientoItem_ActualizarAlmacen
    @IdRequerimiento INT,
    @IdRequerimientoDetalle INT,
    @Cantidad DECIMAL(38,18) = NULL,
    @IdPresupuestoDetalle INT = NULL,
    @Observacion NVARCHAR(MAX) = NULL,
    @CambiarCantidad BIT = 1,
    @CambiarPartida BIT = 1,
    @CambiarObservacion BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @IdRequerimientoDetalle IS NULL OR @IdRequerimientoDetalle <= 0
        THROW 51460, 'IDENTIFICADOR_INVALIDO: IdRequerimientoDetalle debe ser positivo.', 1;
    IF @CambiarCantidad IS NULL OR @CambiarPartida IS NULL OR @CambiarObservacion IS NULL
        OR (@CambiarCantidad = 0 AND @CambiarPartida = 0 AND @CambiarObservacion = 0)
        THROW 51460, 'REQUERIMIENTO_PATCH_INVALIDO: indica al menos un campo editable.', 1;
    IF @CambiarCantidad = 1 AND (@Cantidad IS NULL OR @Cantidad <= 0
        OR @Cantidad > 9999999999999999.99 OR @Cantidad <> ROUND(@Cantidad, 2))
        THROW 51460, 'REQUERIMIENTO_CANTIDAD_INVALIDA: cantidad positiva DECIMAL(18,2), sin redondear.', 1;
    IF @CambiarPartida = 1 AND @IdPresupuestoDetalle <= 0
        THROW 51460, 'IDENTIFICADOR_INVALIDO: IdPresupuestoDetalle debe ser positivo o nulo.', 1;
    IF @CambiarObservacion = 1 AND DATALENGTH(@Observacion) > 500
        THROW 51460, 'LONGITUD_EXCEDIDA: Observacion admite hasta 250 caracteres.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT;
    IF XACT_STATE() = -1 THROW 51469, 'TRANSACCION_NO_CONFIRMABLE: se requiere rollback externo.', 1;
    BEGIN TRY
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE SAVE TRANSACTION ReqActualizarItem;
        DECLARE @PartidaValidar INT = CASE WHEN @CambiarPartida = 1 THEN @IdPresupuestoDetalle END;
        EXEC compras.usp_RequerimientoItem_ValidarGestion @IdRequerimiento, @IdPresupuestoDetalle = @PartidaValidar;
        IF NOT EXISTS (SELECT 1 FROM compras.RequerimientoDetalle
            WHERE IdRequerimiento = @IdRequerimiento AND IdRequerimientoDetalle = @IdRequerimientoDetalle)
            THROW 51464, 'REQUERIMIENTO_ITEM_NO_ENCONTRADO: el ítem no existe en el requerimiento indicado.', 1;
        -- Actualizar solo lo solicitado evita perder cambios concurrentes sobre los campos omitidos.
        UPDATE compras.RequerimientoDetalle
        SET Cantidad = CASE WHEN @CambiarCantidad = 1 THEN @Cantidad ELSE Cantidad END,
            IdPresupuestoDetalle = CASE WHEN @CambiarPartida = 1 THEN @IdPresupuestoDetalle ELSE IdPresupuestoDetalle END,
            Observacion = CASE WHEN @CambiarObservacion = 1 THEN @Observacion ELSE Observacion END
        WHERE IdRequerimiento = @IdRequerimiento AND IdRequerimientoDetalle = @IdRequerimientoDetalle;
        SELECT IdRequerimientoDetalle, IdRequerimiento, IdMaterial, Cantidad, Observacion, IdPresupuestoDetalle
        FROM compras.RequerimientoDetalle
        WHERE IdRequerimiento = @IdRequerimiento AND IdRequerimientoDetalle = @IdRequerimientoDetalle;
        IF @TranCount = 0 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND XACT_STATE() = 1 ROLLBACK TRANSACTION ReqActualizarItem;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE compras.usp_RequerimientoItem_EliminarAlmacen
    @IdRequerimiento INT,
    @IdRequerimientoDetalle INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @IdRequerimientoDetalle IS NULL OR @IdRequerimientoDetalle <= 0
        THROW 51460, 'IDENTIFICADOR_INVALIDO: IdRequerimientoDetalle debe ser positivo.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT;
    IF XACT_STATE() = -1 THROW 51469, 'TRANSACCION_NO_CONFIRMABLE: se requiere rollback externo.', 1;
    BEGIN TRY
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE SAVE TRANSACTION ReqEliminarItem;
        EXEC compras.usp_RequerimientoItem_ValidarGestion @IdRequerimiento;
        IF NOT EXISTS (SELECT 1 FROM compras.RequerimientoDetalle
            WHERE IdRequerimiento = @IdRequerimiento AND IdRequerimientoDetalle = @IdRequerimientoDetalle)
            THROW 51464, 'REQUERIMIENTO_ITEM_NO_ENCONTRADO: el ítem no existe en el requerimiento indicado.', 1;
        IF (SELECT COUNT(*) FROM compras.RequerimientoDetalle WHERE IdRequerimiento = @IdRequerimiento) <= 1
            THROW 51465, 'REQUERIMIENTO_SIN_ITEMS: no se puede eliminar el último ítem.', 1;
        DELETE compras.RequerimientoDetalle
        WHERE IdRequerimiento = @IdRequerimiento AND IdRequerimientoDetalle = @IdRequerimientoDetalle;
        IF @TranCount = 0 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND XACT_STATE() = 1 ROLLBACK TRANSACTION ReqEliminarItem;
        THROW;
    END CATCH;
END;
GO
