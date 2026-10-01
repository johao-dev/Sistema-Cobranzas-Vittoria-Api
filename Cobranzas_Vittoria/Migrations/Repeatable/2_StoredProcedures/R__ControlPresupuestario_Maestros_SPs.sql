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
    @Descripcion NVARCHAR(500) = NULL,
    @IdSeccionGasto INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    BEGIN TRY
        IF @IdSeccionGasto IS NOT NULL AND @IdSeccionGasto <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdSeccionGasto positivo o NULL.', 1;
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
        IF @IdSeccionGasto IS NOT NULL
        BEGIN
            DECLARE @SeccionActiva BIT;
            SELECT @SeccionActiva = Activo FROM ControlPresupuestario.SeccionGasto WITH (HOLDLOCK)
            WHERE IdSeccionGasto = @IdSeccionGasto;
            IF @SeccionActiva IS NULL THROW 51201, 'REFERENCIA_NO_EXISTE: la sección de gasto no existe.', 1;
            IF @SeccionActiva = 0 THROW 51202, 'RECURSO_INACTIVO: la sección de gasto está inactiva.', 1;
        END;
        DECLARE @IdCatalogoPartida INT;
        DECLARE @Nivel INT = 1;
        IF @IdPartidaPadre IS NOT NULL
        BEGIN
            DECLARE @PadreActivo BIT, @NivelPadre INT, @PadreSeccion INT;
            SELECT @PadreActivo = Activo, @NivelPadre = Nivel, @PadreSeccion = IdSeccionGasto
            FROM ControlPresupuestario.CatalogoPartida WHERE IdCatalogoPartida = @IdPartidaPadre;
            IF @PadreActivo IS NULL THROW 51201, 'REFERENCIA_NO_EXISTE: partida padre.', 1;
            IF @PadreActivo = 0 THROW 51212, 'PARTIDA_INACTIVA: padre no disponible.', 1;
            IF @PadreSeccion IS NOT NULL
                THROW 51250, 'SECCION_EN_AGRUPADORA: la partida padre tiene sección de gasto; quítale la sección antes de agregarle partidas hijas.', 1;
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
            (Codigo, Nombre, IdTipoPartida, IdPartidaPadre, Nivel, Descripcion, FechaCreacion, IdSeccionGasto)
        VALUES (@Codigo, @Nombre, @IdTipoPartida, @IdPartidaPadre, @Nivel, @Descripcion, @FechaCreacion,
            @IdSeccionGasto);
        SET @IdCatalogoPartida = CONVERT(INT, SCOPE_IDENTITY());
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdCatalogoPartida AS IdCatalogoPartida, @Codigo AS Codigo, @Nombre AS Nombre,
            @IdTipoPartida AS IdTipoPartida, @IdPartidaPadre AS IdPartidaPadre, @Nivel AS Nivel,
            @Descripcion AS Descripcion, CONVERT(BIT, 1) AS Activo, @FechaCreacion AS FechaCreacion,
            @IdSeccionGasto AS IdSeccionGasto;
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
    @Descripcion NVARCHAR(500) = NULL,
    @IdSeccionGasto INT = NULL
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
        IF @IdSeccionGasto IS NOT NULL AND @IdSeccionGasto <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdSeccionGasto positivo o NULL.', 1;
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
        -- Solo una hoja puede pertenecer a una sección de gasto, y un padre con
        -- sección no puede recibir hijas (dejaría de ser hoja).
        IF @IdSeccionGasto IS NOT NULL
        BEGIN
            DECLARE @SeccionActiva BIT, @SeccionAnterior INT;
            SELECT @SeccionAnterior = IdSeccionGasto FROM ControlPresupuestario.CatalogoPartida
            WHERE IdCatalogoPartida = @IdCatalogoPartida;
            SELECT @SeccionActiva = Activo FROM ControlPresupuestario.SeccionGasto WITH (HOLDLOCK)
            WHERE IdSeccionGasto = @IdSeccionGasto;
            IF @SeccionActiva IS NULL THROW 51201, 'REFERENCIA_NO_EXISTE: la sección de gasto no existe.', 1;
            -- Conservar una sección que se desactivó después sigue permitido.
            IF @SeccionActiva = 0 AND ISNULL(@SeccionAnterior, 0) <> @IdSeccionGasto
                THROW 51202, 'RECURSO_INACTIVO: la sección de gasto está inactiva.', 1;
            IF EXISTS (SELECT 1 FROM ControlPresupuestario.CatalogoPartida WHERE IdPartidaPadre = @IdCatalogoPartida)
                THROW 51250, 'SECCION_EN_AGRUPADORA: solo una partida sin hijas puede pertenecer a una sección de gasto.', 1;
        END;
        IF @IdPartidaPadre IS NOT NULL AND EXISTS (
            SELECT 1 FROM ControlPresupuestario.CatalogoPartida
            WHERE IdCatalogoPartida = @IdPartidaPadre AND IdSeccionGasto IS NOT NULL)
            THROW 51250, 'SECCION_EN_AGRUPADORA: la partida padre tiene sección de gasto; quítale la sección antes de agregarle partidas hijas.', 1;
        DECLARE @FechaActualizacion DATETIME2(0) = SYSDATETIME();
        UPDATE ControlPresupuestario.CatalogoPartida
        SET Nombre = @Nombre, Descripcion = @Descripcion, IdTipoPartida = @IdTipoPartida,
            IdPartidaPadre = @IdPartidaPadre, Nivel = @Nivel, Activo = @Activo,
            IdSeccionGasto = @IdSeccionGasto, FechaActualizacion = @FechaActualizacion
        WHERE IdCatalogoPartida = @IdCatalogoPartida;
        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @IdCatalogoPartida AS IdCatalogoPartida, @Nombre AS Nombre, @Descripcion AS Descripcion,
            @IdTipoPartida AS IdTipoPartida, @IdPartidaPadre AS IdPartidaPadre, @Nivel AS Nivel,
            @Activo AS Activo, @IdSeccionGasto AS IdSeccionGasto, @FechaActualizacion AS FechaActualizacion;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_CatalogoPartidaActualizar;
        THROW;
    END CATCH;
END;
GO

/*
    Importación masiva del catálogo de partidas (todo o nada).

    La API ya validó cada fila y devolvió los errores por fila; aquí se repiten
    las reglas como defensa y se resuelve la jerarquía: el CSV puede traer las
    hijas antes que sus padres, así que se inserta por oleadas (primero las filas
    cuyo padre ya existe en la tabla, luego sus hijas, etc.). Si una oleada no
    avanza, las filas restantes forman un ciclo.

    Códigos 50001-50099: el motor de importación los traduce a 422 con detalle.
*/
CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_CatalogoPartida_CargaMasiva
    @Filas ControlPresupuestario.TVP_CatalogoPartida READONLY,
    @Usuario VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    DECLARE @Mensaje NVARCHAR(2048);
    BEGIN TRY
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_CatalogoPartidaCarga;
            SET @SavepointCreado = 1;
        END;

        -- Mismo criterio de bloqueo que el alta individual: el árbol completo.
        DECLARE @CantidadPartidas BIGINT;
        SELECT @CantidadPartidas = COUNT_BIG(*) FROM ControlPresupuestario.CatalogoPartida WITH (TABLOCKX, HOLDLOCK);

        DECLARE @Pendientes TABLE
        (
            Codigo VARCHAR(50) NOT NULL PRIMARY KEY,
            Nombre NVARCHAR(200) NOT NULL,
            IdTipoPartida INT NOT NULL,
            CodigoPadre VARCHAR(50) NULL,
            IdSeccionGasto INT NULL,
            Descripcion NVARCHAR(500) NULL,
            Fila INT NOT NULL
        );

        SELECT TOP (1) @Mensaje = CONCAT(N'CAMPO_OBLIGATORIO: fila ', _Fila, N', código y nombre son obligatorios.')
        FROM @Filas
        WHERE NULLIF(LTRIM(RTRIM(Codigo)), '') IS NULL OR NULLIF(LTRIM(RTRIM(Nombre)), N'') IS NULL
        ORDER BY _Fila;
        IF @Mensaje IS NOT NULL THROW 50001, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'VALOR_DUPLICADO_EN_ARCHIVO: el código ', LTRIM(RTRIM(Codigo)),
            N' se repite en el archivo.')
        FROM @Filas GROUP BY LTRIM(RTRIM(Codigo)) HAVING COUNT(*) > 1 ORDER BY LTRIM(RTRIM(Codigo));
        IF @Mensaje IS NOT NULL THROW 50002, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'VALOR_YA_EXISTE_EN_BD: fila ', f._Fila, N', el código ', p.Codigo,
            N' ya existe en el catálogo.')
        FROM @Filas f JOIN ControlPresupuestario.CatalogoPartida p ON p.Codigo = LTRIM(RTRIM(f.Codigo))
        ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50003, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'FK_NO_EXISTE: fila ', f._Fila, N', el tipo de partida no existe o está inactivo.')
        FROM @Filas f
        LEFT JOIN ControlPresupuestario.TipoPartida t ON t.IdTipoPartida = f.IdTipoPartida AND t.Activo = 1
        WHERE t.IdTipoPartida IS NULL ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50004, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'FK_NO_EXISTE: fila ', f._Fila, N', la sección de gasto no existe o está inactiva.')
        FROM @Filas f
        LEFT JOIN ControlPresupuestario.SeccionGasto s ON s.IdSeccionGasto = f.IdSeccionGasto AND s.Activo = 1
        WHERE f.IdSeccionGasto IS NOT NULL AND s.IdSeccionGasto IS NULL ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50004, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'FK_NO_EXISTE: fila ', f._Fila, N', la partida padre ',
            LTRIM(RTRIM(f.CodigoPadre)), N' no existe en el catálogo ni en el archivo.')
        FROM @Filas f
        WHERE NULLIF(LTRIM(RTRIM(f.CodigoPadre)), '') IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM @Filas x WHERE LTRIM(RTRIM(x.Codigo)) = LTRIM(RTRIM(f.CodigoPadre)))
          AND NOT EXISTS (SELECT 1 FROM ControlPresupuestario.CatalogoPartida p
                          WHERE p.Codigo = LTRIM(RTRIM(f.CodigoPadre)))
        ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50004, @Mensaje, 1;

        -- Padre existente en BD: debe estar activo, sin sección y sin montos.
        SELECT TOP (1) @Mensaje = CONCAT(N'PADRE_NO_DISPONIBLE: fila ', f._Fila, N', la partida padre ', p.Codigo,
            CASE
                WHEN p.Activo = 0 THEN N' está inactiva.'
                WHEN p.IdSeccionGasto IS NOT NULL THEN N' tiene sección de gasto y no puede tener hijas.'
                ELSE N' ya tiene montos presupuestados y no puede convertirse en agrupadora.'
            END)
        FROM @Filas f
        JOIN ControlPresupuestario.CatalogoPartida p ON p.Codigo = LTRIM(RTRIM(f.CodigoPadre))
        WHERE p.Activo = 0 OR p.IdSeccionGasto IS NOT NULL
           OR EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle d WITH (HOLDLOCK)
                      WHERE d.IdCatalogoPartida = p.IdCatalogoPartida)
        ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50005, @Mensaje, 1;

        -- Una fila con sección no puede ser padre de otra fila del archivo.
        SELECT TOP (1) @Mensaje = CONCAT(N'SECCION_EN_AGRUPADORA: fila ', f._Fila, N', la partida ',
            LTRIM(RTRIM(f.Codigo)), N' tiene hijas en el archivo; solo una partida sin hijas puede tener sección de gasto.')
        FROM @Filas f
        WHERE f.IdSeccionGasto IS NOT NULL
          AND EXISTS (SELECT 1 FROM @Filas h WHERE LTRIM(RTRIM(h.CodigoPadre)) = LTRIM(RTRIM(f.Codigo)))
        ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50006, @Mensaje, 1;

        INSERT INTO @Pendientes (Codigo, Nombre, IdTipoPartida, CodigoPadre, IdSeccionGasto, Descripcion, Fila)
        SELECT LTRIM(RTRIM(Codigo)), LTRIM(RTRIM(Nombre)), IdTipoPartida,
            NULLIF(LTRIM(RTRIM(CodigoPadre)), ''), IdSeccionGasto, NULLIF(LTRIM(RTRIM(Descripcion)), N''), _Fila
        FROM @Filas;

        DECLARE @Total INT = 0, @Oleada INT;
        DECLARE @Fecha DATETIME2(0) = SYSDATETIME();
        WHILE EXISTS (SELECT 1 FROM @Pendientes)
        BEGIN
            INSERT INTO ControlPresupuestario.CatalogoPartida
                (Codigo, Nombre, IdTipoPartida, IdPartidaPadre, Nivel, Descripcion, FechaCreacion, IdSeccionGasto)
            SELECT pe.Codigo, pe.Nombre, pe.IdTipoPartida, padre.IdCatalogoPartida,
                ISNULL(padre.Nivel, 0) + 1, pe.Descripcion, @Fecha, pe.IdSeccionGasto
            FROM @Pendientes pe
            LEFT JOIN ControlPresupuestario.CatalogoPartida padre ON padre.Codigo = pe.CodigoPadre
            WHERE pe.CodigoPadre IS NULL OR padre.IdCatalogoPartida IS NOT NULL;

            SET @Oleada = @@ROWCOUNT;
            IF @Oleada = 0
            BEGIN
                SELECT TOP (1) @Mensaje = CONCAT(N'JERARQUIA_INVALIDA: fila ', Fila, N', la partida ', Codigo,
                    N' forma un ciclo con su partida padre.')
                FROM @Pendientes ORDER BY Fila;
                THROW 50007, @Mensaje, 1;
            END;
            SET @Total = @Total + @Oleada;

            DELETE pe FROM @Pendientes pe
            WHERE EXISTS (SELECT 1 FROM ControlPresupuestario.CatalogoPartida p WHERE p.Codigo = pe.Codigo);
        END;

        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @Total AS FilasInsertadas;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_CatalogoPartidaCarga;
        THROW;
    END CATCH;
END;
GO

/* Importación masiva de centros de costo (todo o nada). La API ya resolvió tipo
y proyecto e informó los errores por fila; aquí se revalidan las mismas reglas
del alta individual bajo bloqueo, por si otro usuario cambió los datos entre medio. */
CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_CentroCosto_CargaMasiva
    @Filas ControlPresupuestario.TVP_CentroCosto READONLY,
    @Usuario VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF XACT_STATE() = -1
        THROW 51240, 'TRANSACCION_NO_CONFIRMABLE: la unidad externa requiere rollback.', 1;
    DECLARE @TranCount INT = @@TRANCOUNT, @SavepointCreado BIT = 0;
    DECLARE @Mensaje NVARCHAR(2048);
    BEGIN TRY
        IF @TranCount = 0 BEGIN TRANSACTION;
        ELSE
        BEGIN
            SAVE TRANSACTION CP_CentroCostoCarga;
            SET @SavepointCreado = 1;
        END;

        DECLARE @Bloqueo INT;
        SELECT @Bloqueo = COUNT(*) FROM ControlPresupuestario.CentroCosto WITH (UPDLOCK, HOLDLOCK);

        SELECT TOP (1) @Mensaje = CONCAT(N'CAMPO_OBLIGATORIO: fila ', _Fila, N', código y nombre son obligatorios.')
        FROM @Filas
        WHERE NULLIF(LTRIM(RTRIM(Codigo)), '') IS NULL OR NULLIF(LTRIM(RTRIM(Nombre)), N'') IS NULL
        ORDER BY _Fila;
        IF @Mensaje IS NOT NULL THROW 50001, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'VALOR_DUPLICADO_EN_ARCHIVO: el código ', LTRIM(RTRIM(Codigo)),
            N' se repite en el archivo.')
        FROM @Filas GROUP BY LTRIM(RTRIM(Codigo)) HAVING COUNT(*) > 1 ORDER BY LTRIM(RTRIM(Codigo));
        IF @Mensaje IS NOT NULL THROW 50002, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'VALOR_DUPLICADO_EN_ARCHIVO: fila ', MAX(_Fila),
            N', el proyecto ya está asignado a otra fila del archivo.')
        FROM @Filas WHERE IdProyecto IS NOT NULL GROUP BY IdProyecto HAVING COUNT(*) > 1;
        IF @Mensaje IS NOT NULL THROW 50002, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'VALOR_YA_EXISTE_EN_BD: fila ', f._Fila, N', el código ', c.Codigo,
            N' ya existe.')
        FROM @Filas f JOIN ControlPresupuestario.CentroCosto c ON c.Codigo = LTRIM(RTRIM(f.Codigo))
        ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50003, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'VALOR_YA_EXISTE_EN_BD: fila ', f._Fila,
            N', el proyecto ya tiene el centro de costo ', c.Codigo, N'.')
        FROM @Filas f JOIN ControlPresupuestario.CentroCosto c ON c.IdProyecto = f.IdProyecto
        ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50003, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'FK_NO_EXISTE: fila ', f._Fila, N', el tipo de centro de costo no existe o está inactivo.')
        FROM @Filas f
        LEFT JOIN ControlPresupuestario.TipoCentroCosto t WITH (HOLDLOCK)
            ON t.IdTipoCentroCosto = f.IdTipoCentroCosto AND t.Activo = 1
        WHERE t.IdTipoCentroCosto IS NULL ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50004, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'FK_NO_EXISTE: fila ', f._Fila, N', el proyecto no existe o está inactivo.')
        FROM @Filas f
        LEFT JOIN maestra.Proyecto p WITH (HOLDLOCK) ON p.IdProyecto = f.IdProyecto AND p.Activo = 1
        WHERE f.IdProyecto IS NOT NULL AND p.IdProyecto IS NULL ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50004, @Mensaje, 1;

        SELECT TOP (1) @Mensaje = CONCAT(N'DOMINIO_CENTRO_COSTO_INVALIDO: fila ', f._Fila,
            CASE WHEN t.Codigo = 'PROYECTO' THEN N', el tipo PROYECTO requiere proyecto.'
                 ELSE N', solo el tipo PROYECTO admite proyecto.' END)
        FROM @Filas f JOIN ControlPresupuestario.TipoCentroCosto t ON t.IdTipoCentroCosto = f.IdTipoCentroCosto
        WHERE (t.Codigo = 'PROYECTO' AND f.IdProyecto IS NULL) OR (t.Codigo <> 'PROYECTO' AND f.IdProyecto IS NOT NULL)
        ORDER BY f._Fila;
        IF @Mensaje IS NOT NULL THROW 50005, @Mensaje, 1;

        DECLARE @Fecha DATETIME2(0) = SYSUTCDATETIME();
        INSERT INTO ControlPresupuestario.CentroCosto
            (Codigo, Nombre, IdTipoCentroCosto, IdProyecto, Descripcion, FechaCreacion, FechaModificacion)
        SELECT LTRIM(RTRIM(Codigo)), LTRIM(RTRIM(Nombre)), IdTipoCentroCosto, IdProyecto,
            NULLIF(LTRIM(RTRIM(Descripcion)), N''), @Fecha, NULL
        FROM @Filas;
        DECLARE @Total INT = @@ROWCOUNT;

        IF @TranCount = 0 COMMIT TRANSACTION;
        SELECT @Total AS FilasInsertadas;
    END TRY
    BEGIN CATCH
        IF @TranCount = 0 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        ELSE IF @TranCount > 0 AND @SavepointCreado = 1 AND XACT_STATE() = 1
            ROLLBACK TRANSACTION CP_CentroCostoCarga;
        THROW;
    END CATCH;
END;
GO
