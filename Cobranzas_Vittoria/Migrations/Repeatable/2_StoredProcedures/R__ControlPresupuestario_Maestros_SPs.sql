/* Administración de maestros. Códigos estables, sin DELETE.
CatalogoPartida serializa sus cambios administrativos con TABLOCKX/HOLDLOCK.
Los escritores presupuestarios conservan sus lecturas HOLDLOCK de los maestros.
No se actualizan versiones, detalles ni ledger desde estos procedimientos.
*/

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_CentroCosto_Crear
    @Codigo VARCHAR(30),
    @Nombre NVARCHAR(150),
    @IdTipoCentroCosto INT,
    @Descripcion NVARCHAR(255) = NULL,
    @IdProyecto INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        SET @Codigo = LTRIM(RTRIM(@Codigo));
        IF NULLIF(@Codigo, '') IS NULL THROW 51200, 'CAMPO_REQUERIDO: Codigo.', 1;
        SET @Nombre = LTRIM(RTRIM(@Nombre));
        IF NULLIF(@Nombre, N'') IS NULL THROW 51200, 'CAMPO_REQUERIDO: Nombre.', 1;
        IF @IdTipoCentroCosto IS NULL OR @IdTipoCentroCosto <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdTipoCentroCosto positivo.', 1;
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_CentroCostoCrear;
            SET @SavepointCreado = 1;
        END;
        DECLARE @TipoActivo BIT, @CodigoTipo VARCHAR(30);
        SELECT @TipoActivo = Activo, @CodigoTipo = Codigo FROM ControlPresupuestario.TipoCentroCosto WITH (HOLDLOCK)
        WHERE IdTipoCentroCosto = @IdTipoCentroCosto;
        IF @TipoActivo IS NULL THROW 51201, 'REFERENCIA_NO_EXISTE: TipoCentroCosto.', 1;
        IF @TipoActivo = 0 THROW 51202, 'RECURSO_INACTIVO: TipoCentroCosto.', 1;
        IF @CodigoTipo = 'PROYECTO' AND @IdProyecto IS NULL
            THROW 51200, 'CAMPO_REQUERIDO: un CentroCosto de tipo PROYECTO requiere IdProyecto.', 1;
        IF @IdProyecto IS NOT NULL AND @CodigoTipo <> 'PROYECTO'
            THROW 51220, 'DOMINIO_CENTRO_COSTO_INVALIDO: solo el tipo PROYECTO admite IdProyecto.', 1;
        IF @IdProyecto IS NOT NULL AND NOT EXISTS
            (SELECT 1 FROM maestra.Proyecto WITH (HOLDLOCK) WHERE IdProyecto = @IdProyecto AND Activo = 1)
            THROW 51201, 'REFERENCIA_NO_EXISTE: Proyecto activo.', 1;
        IF @IdProyecto IS NOT NULL AND EXISTS (SELECT 1 FROM ControlPresupuestario.CentroCosto WITH
            (UPDLOCK, HOLDLOCK, INDEX(UX_CentroCosto_IdProyecto)) WHERE IdProyecto = @IdProyecto)
            THROW 51205, 'PROYECTO_DUPLICADO: ya está asociado a otro CentroCosto.', 1;
        IF EXISTS (SELECT 1 FROM ControlPresupuestario.CentroCosto WITH
            (UPDLOCK, HOLDLOCK, INDEX(UQ_CentroCosto_Codigo)) WHERE Codigo = @Codigo)
            THROW 51205, 'CODIGO_DUPLICADO: CentroCosto.', 1;
        DECLARE @IdCentroCosto INT, @FechaCreacion DATETIME2(0) = SYSUTCDATETIME();
        INSERT INTO ControlPresupuestario.CentroCosto
            (Codigo, Nombre, IdTipoCentroCosto, IdProyecto, Descripcion, FechaCreacion, FechaModificacion)
        VALUES (@Codigo, @Nombre, @IdTipoCentroCosto, @IdProyecto, @Descripcion, @FechaCreacion, NULL);
        SET @IdCentroCosto = CONVERT(INT, SCOPE_IDENTITY());
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdCentroCosto AS IdCentroCosto, @Codigo AS Codigo, @Nombre AS Nombre,
            @IdTipoCentroCosto AS IdTipoCentroCosto, @IdProyecto AS IdProyecto, @Descripcion AS Descripcion,
            CONVERT(BIT, 1) AS Activo, @FechaCreacion AS FechaCreacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_CentroCostoCrear;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_CentroCosto_Actualizar
    @IdCentroCosto INT,
    @Nombre NVARCHAR(150),
    @Activo BIT,
    @Descripcion NVARCHAR(255) = NULL,
    @IdProyecto INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @IdCentroCosto IS NULL OR @IdCentroCosto <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdCentroCosto positivo.', 1;
        SET @Nombre = LTRIM(RTRIM(@Nombre));
        IF NULLIF(@Nombre, N'') IS NULL THROW 51200, 'CAMPO_REQUERIDO: Nombre.', 1;
        IF @Activo IS NULL THROW 51200, 'CAMPO_REQUERIDO: Activo.', 1;
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_CentroCostoActualizar;
            SET @SavepointCreado = 1;
        END;
        DECLARE @IdTipoCentroCosto INT, @IdProyectoActual INT, @CodigoTipo VARCHAR(30);
        SELECT @IdTipoCentroCosto = cc.IdTipoCentroCosto, @IdProyectoActual = cc.IdProyecto,
            @CodigoTipo = t.Codigo
        FROM ControlPresupuestario.CentroCosto cc WITH (UPDLOCK, HOLDLOCK, INDEX(PK_CentroCosto))
        JOIN ControlPresupuestario.TipoCentroCosto t ON t.IdTipoCentroCosto = cc.IdTipoCentroCosto
        WHERE cc.IdCentroCosto = @IdCentroCosto;
        IF @IdTipoCentroCosto IS NULL
            THROW 51203, 'RECURSO_NO_EXISTE: CentroCosto.', 1;
        SET @IdProyecto = COALESCE(@IdProyecto, @IdProyectoActual);
        IF @IdProyecto IS NOT NULL AND @CodigoTipo <> 'PROYECTO'
            THROW 51220, 'DOMINIO_CENTRO_COSTO_INVALIDO: solo el tipo PROYECTO admite IdProyecto.', 1;
        IF @IdProyecto IS NOT NULL AND NOT EXISTS
            (SELECT 1 FROM maestra.Proyecto WITH (HOLDLOCK) WHERE IdProyecto = @IdProyecto AND Activo = 1)
            THROW 51201, 'REFERENCIA_NO_EXISTE: Proyecto activo.', 1;
        IF @IdProyecto IS NOT NULL AND EXISTS (SELECT 1 FROM ControlPresupuestario.CentroCosto WITH
            (UPDLOCK, HOLDLOCK, INDEX(UX_CentroCosto_IdProyecto))
            WHERE IdProyecto = @IdProyecto AND IdCentroCosto <> @IdCentroCosto)
            THROW 51205, 'PROYECTO_DUPLICADO: ya está asociado a otro CentroCosto.', 1;
        DECLARE @FechaModificacion DATETIME2(0) = SYSUTCDATETIME();
        UPDATE ControlPresupuestario.CentroCosto
        SET Nombre = @Nombre, Descripcion = @Descripcion, Activo = @Activo, IdProyecto = @IdProyecto,
            FechaModificacion = @FechaModificacion
        WHERE IdCentroCosto = @IdCentroCosto;
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdCentroCosto AS IdCentroCosto, @Nombre AS Nombre, @Descripcion AS Descripcion,
            @IdProyecto AS IdProyecto, @Activo AS Activo, @FechaModificacion AS FechaModificacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_CentroCostoActualizar;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_CatalogoPartida_Crear
    @Codigo VARCHAR(50),
    @Nombre NVARCHAR(200),
    @IdTipoPartida INT,
    @IdPartidaPadre INT = NULL,
    @Descripcion NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        SET @Codigo = LTRIM(RTRIM(@Codigo));
        IF NULLIF(@Codigo, '') IS NULL THROW 51200, 'CAMPO_REQUERIDO: Codigo.', 1;
        SET @Nombre = LTRIM(RTRIM(@Nombre));
        IF NULLIF(@Nombre, N'') IS NULL THROW 51200, 'CAMPO_REQUERIDO: Nombre.', 1;
        IF @IdTipoPartida IS NULL OR @IdTipoPartida <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdTipoPartida positivo.', 1;
        IF @IdPartidaPadre IS NOT NULL AND @IdPartidaPadre <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPartidaPadre positivo o NULL.', 1;
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_CatalogoPartidaCrear;
            SET @SavepointCreado = 1;
        END;
        -- Administración poco frecuente: protege también árboles vacíos y ancestros.
        -- No tomar después un mutex de Presupuesto ni actualizar sus snapshots.
        DECLARE @CantidadPartidas BIGINT;
        SELECT @CantidadPartidas = COUNT_BIG(*) FROM ControlPresupuestario.CatalogoPartida WITH (TABLOCKX, HOLDLOCK);
        DECLARE @TipoActivo BIT;
        SELECT @TipoActivo = Activo FROM ControlPresupuestario.TipoPartida WITH (HOLDLOCK)
        WHERE IdTipoPartida = @IdTipoPartida;
        IF @TipoActivo IS NULL THROW 51201, 'REFERENCIA_NO_EXISTE: TipoPartida.', 1;
        IF @TipoActivo = 0 THROW 51202, 'RECURSO_INACTIVO: TipoPartida.', 1;
        IF EXISTS (SELECT 1 FROM ControlPresupuestario.CatalogoPartida WHERE Codigo = @Codigo)
            THROW 51205, 'CODIGO_DUPLICADO: CatalogoPartida.', 1;
        DECLARE @IdCatalogoPartida INT;
        DECLARE @Nivel INT = 1;
        IF @IdPartidaPadre IS NOT NULL
        BEGIN
            DECLARE @PadreActivo BIT, @NivelPadre INT;
            SELECT @PadreActivo = Activo, @NivelPadre = Nivel
            FROM ControlPresupuestario.CatalogoPartida WHERE IdCatalogoPartida = @IdPartidaPadre;
            IF @PadreActivo IS NULL THROW 51201, 'REFERENCIA_NO_EXISTE: partida padre.', 1;
            IF @PadreActivo = 0 THROW 51212, 'PARTIDA_INACTIVA: padre no disponible.', 1;
            IF @NivelPadre = 2147483647 THROW 51217, 'JERARQUIA_INVALIDA: nivel fuera de rango.', 1;
            SET @Nivel = @NivelPadre + 1;
            IF EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle WITH
                (HOLDLOCK, INDEX(IX_PresupuestoDetalle_IdCatalogoPartida)) WHERE IdCatalogoPartida = @IdPartidaPadre)
                THROW 51218, 'ESTRUCTURA_EN_USO: una partida presupuestada no puede convertirse en padre.', 1;
        END;
        -- Sin límite arbitrario de recursión; también detecta ciclos preexistentes.
        DECLARE @Ancestro INT = @IdPartidaPadre, @Siguiente INT, @NivelAncestro INT,
            @NivelEsperado INT = @Nivel - 1;
        DECLARE @Visitados TABLE (Id INT PRIMARY KEY);
        WHILE @Ancestro IS NOT NULL
        BEGIN
            IF @Ancestro = @IdCatalogoPartida OR EXISTS (SELECT 1 FROM @Visitados WHERE Id = @Ancestro)
                THROW 51217, 'JERARQUIA_INVALIDA: autorreferencia o ciclo.', 1;
            INSERT INTO @Visitados (Id) VALUES (@Ancestro);
            SET @NivelAncestro = NULL;
            SELECT @Siguiente = IdPartidaPadre, @NivelAncestro = Nivel
            FROM ControlPresupuestario.CatalogoPartida WHERE IdCatalogoPartida = @Ancestro;
            IF @NivelAncestro IS NULL OR @NivelAncestro <> @NivelEsperado
                THROW 51217, 'JERARQUIA_INVALIDA: niveles de ancestros incoherentes.', 1;
            SET @NivelEsperado = @NivelEsperado - 1;
            SET @Ancestro = @Siguiente;
        END;
        IF @NivelEsperado <> 0 THROW 51217, 'JERARQUIA_INVALIDA: la raíz debe tener nivel uno.', 1;
        DECLARE @FechaCreacion DATETIME2(0) = SYSDATETIME();
        INSERT INTO ControlPresupuestario.CatalogoPartida
            (Codigo, Nombre, IdTipoPartida, IdPartidaPadre, Nivel, Descripcion, FechaCreacion)
        VALUES (@Codigo, @Nombre, @IdTipoPartida, @IdPartidaPadre, @Nivel, @Descripcion, @FechaCreacion);
        SET @IdCatalogoPartida = CONVERT(INT, SCOPE_IDENTITY());
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdCatalogoPartida AS IdCatalogoPartida, @Codigo AS Codigo, @Nombre AS Nombre,
            @IdTipoPartida AS IdTipoPartida, @IdPartidaPadre AS IdPartidaPadre, @Nivel AS Nivel,
            @Descripcion AS Descripcion, CONVERT(BIT, 1) AS Activo, @FechaCreacion AS FechaCreacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_CatalogoPartidaCrear;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_CatalogoPartida_Actualizar
    @IdCatalogoPartida INT,
    @Nombre NVARCHAR(200),
    @IdTipoPartida INT,
    @Activo BIT,
    @IdPartidaPadre INT = NULL,
    @Descripcion NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @IdCatalogoPartida IS NULL OR @IdCatalogoPartida <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdCatalogoPartida positivo.', 1;
        IF @IdTipoPartida IS NULL OR @IdTipoPartida <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdTipoPartida positivo.', 1;
        SET @Nombre = LTRIM(RTRIM(@Nombre));
        IF NULLIF(@Nombre, N'') IS NULL THROW 51200, 'CAMPO_REQUERIDO: Nombre.', 1;
        IF @Activo IS NULL THROW 51200, 'CAMPO_REQUERIDO: Activo.', 1;
        IF @IdPartidaPadre IS NOT NULL AND @IdPartidaPadre <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPartidaPadre positivo o NULL.', 1;
        IF @IdPartidaPadre = @IdCatalogoPartida
            THROW 51217, 'JERARQUIA_INVALIDA: una partida no puede ser su propio padre.', 1;
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_CatalogoPartidaActualizar;
            SET @SavepointCreado = 1;
        END;
        -- Administración poco frecuente: protege también árboles vacíos y ancestros.
        -- No tomar después un mutex de Presupuesto ni actualizar sus snapshots.
        DECLARE @CantidadPartidas BIGINT;
        SELECT @CantidadPartidas = COUNT_BIG(*) FROM ControlPresupuestario.CatalogoPartida WITH (TABLOCKX, HOLDLOCK);
        DECLARE @TipoAnterior INT, @PadreAnterior INT, @NivelAnterior INT;
        SELECT @TipoAnterior = IdTipoPartida, @PadreAnterior = IdPartidaPadre, @NivelAnterior = Nivel
        FROM ControlPresupuestario.CatalogoPartida WHERE IdCatalogoPartida = @IdCatalogoPartida;
        IF @TipoAnterior IS NULL THROW 51203, 'RECURSO_NO_EXISTE: CatalogoPartida.', 1;
        DECLARE @CambioEstructura BIT = CASE WHEN @TipoAnterior <> @IdTipoPartida
            OR ISNULL(@PadreAnterior, 0) <> ISNULL(@IdPartidaPadre, 0) THEN 1 ELSE 0 END;
        -- Una edición de texto/Activo sigue permitida aunque padre/tipo se hayan retirado.
        DECLARE @Nivel INT = @NivelAnterior;
        IF @CambioEstructura = 1
        BEGIN
            DECLARE @TipoActivo BIT;
            SELECT @TipoActivo = Activo FROM ControlPresupuestario.TipoPartida WITH (HOLDLOCK)
            WHERE IdTipoPartida = @IdTipoPartida;
            IF @TipoActivo IS NULL THROW 51201, 'REFERENCIA_NO_EXISTE: TipoPartida.', 1;
            IF @TipoActivo = 0 THROW 51202, 'RECURSO_INACTIVO: TipoPartida.', 1;
            -- Validar ciclo antes de informar la protección de una rama usada.
            DECLARE @Ancestro INT = @IdPartidaPadre;
            DECLARE @Visitados TABLE (Id INT PRIMARY KEY);
            WHILE @Ancestro IS NOT NULL
            BEGIN
                IF @Ancestro = @IdCatalogoPartida OR EXISTS (SELECT 1 FROM @Visitados WHERE Id = @Ancestro)
                    THROW 51217, 'JERARQUIA_INVALIDA: autorreferencia o ciclo.', 1;
                INSERT INTO @Visitados (Id) VALUES (@Ancestro);
                DECLARE @Proximo INT = NULL;
                SELECT @Proximo = IdPartidaPadre FROM ControlPresupuestario.CatalogoPartida
                WHERE IdCatalogoPartida = @Ancestro;
                SET @Ancestro = @Proximo;
            END;
            IF EXISTS (SELECT 1 FROM ControlPresupuestario.CatalogoPartida WHERE IdPartidaPadre = @IdCatalogoPartida)
                OR EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle WITH
                    (HOLDLOCK, INDEX(IX_PresupuestoDetalle_IdCatalogoPartida)) WHERE IdCatalogoPartida = @IdCatalogoPartida)
                THROW 51218, 'ESTRUCTURA_EN_USO: solo se reclasifican hojas sin detalles de ninguna versión.', 1;
            SET @Nivel = 1;
            IF @IdPartidaPadre IS NOT NULL
            BEGIN
                DECLARE @PadreActivo BIT, @NivelPadre INT;
                SELECT @PadreActivo = Activo, @NivelPadre = Nivel
                FROM ControlPresupuestario.CatalogoPartida WHERE IdCatalogoPartida = @IdPartidaPadre;
                IF @PadreActivo IS NULL THROW 51201, 'REFERENCIA_NO_EXISTE: partida padre.', 1;
                IF @PadreActivo = 0 THROW 51212, 'PARTIDA_INACTIVA: padre no disponible.', 1;
                IF @NivelPadre = 2147483647 THROW 51217, 'JERARQUIA_INVALIDA: nivel fuera de rango.', 1;
                SET @Nivel = @NivelPadre + 1;
                IF EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle WITH
                    (HOLDLOCK, INDEX(IX_PresupuestoDetalle_IdCatalogoPartida)) WHERE IdCatalogoPartida = @IdPartidaPadre)
                    THROW 51218, 'ESTRUCTURA_EN_USO: una partida presupuestada no puede convertirse en padre.', 1;
            END;
            DECLARE @NivelEsperado INT = @Nivel - 1, @NivelLeido INT;
            SET @Ancestro = @IdPartidaPadre;
            WHILE @Ancestro IS NOT NULL
            BEGIN
                SET @NivelLeido = NULL;
                SELECT @NivelLeido = Nivel, @Ancestro = IdPartidaPadre
                FROM ControlPresupuestario.CatalogoPartida WHERE IdCatalogoPartida = @Ancestro;
                IF @NivelLeido IS NULL OR @NivelLeido <> @NivelEsperado
                    THROW 51217, 'JERARQUIA_INVALIDA: niveles de ancestros incoherentes.', 1;
                SET @NivelEsperado = @NivelEsperado - 1;
            END;
            IF @NivelEsperado <> 0 THROW 51217, 'JERARQUIA_INVALIDA: la raíz debe tener nivel uno.', 1;
        END;
        DECLARE @FechaActualizacion DATETIME2(0) = SYSDATETIME();
        UPDATE ControlPresupuestario.CatalogoPartida
        SET Nombre = @Nombre, Descripcion = @Descripcion, IdTipoPartida = @IdTipoPartida,
            IdPartidaPadre = @IdPartidaPadre, Nivel = @Nivel, Activo = @Activo,
            FechaActualizacion = @FechaActualizacion
        WHERE IdCatalogoPartida = @IdCatalogoPartida;
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdCatalogoPartida AS IdCatalogoPartida, @Nombre AS Nombre, @Descripcion AS Descripcion,
            @IdTipoPartida AS IdTipoPartida, @IdPartidaPadre AS IdPartidaPadre, @Nivel AS Nivel,
            @Activo AS Activo, @FechaActualizacion AS FechaActualizacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_CatalogoPartidaActualizar;
        THROW;
    END CATCH;
END;
GO
