/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.6.3
Módulo      : Contable / Gasto directo
Descripción : Migra a contable.GastoDirecto los gastos históricos de
              contable.GastoProyecto y contable.GastoAdministrativo que V2.3.1
              no pudo copiar porque no tenían partida presupuestal
              (IdPresupuestoDetalle NULL) o su moneda no coincidía con la del
              presupuesto de su partida.
===============================================================================

Cada gasto necesita una partida de presupuesto (GastoDirecto.IdPresupuestoDetalle
es obligatorio). Para no inventar consumo sobre los presupuestos reales, los
históricos se cuelgan de un archivo aparte:

1) Catálogo: categoría raíz HIST "Gastos históricos (migrados)" con una hoja por
   sección de gasto (HIST.TERRENO, HIST.MUNICIPAL, …) que lleva esa sección.
2) Centro de costo: el del proyecto del gasto (se crea CC-PRY-<id> si el proyecto
   no tiene uno); un gasto administrativo sin proyecto va al primer centro de
   ADMINISTRACION (se crea CC-ADM-HIST si no hay).
3) Presupuesto HIST-<centro>-<moneda>, INACTIVO, con su versión 1 APROBADA y
   monto 0 en cada partida histórica. Al estar inactivo no entra en los reportes
   ni en el tablero y no admite gastos nuevos; solo anular.
4) Los gastos se copian como en V2.3.1: sin movimientos presupuestales (no alteran
   saldos ni ejecución), estado REGISTRADO si estaban activos y ANULADO si no,
   con su proveedor canónico (ProveedorLegacyMap) y sus documentos. Sección:
   TipoModulo del gasto de proyecto o categoría del gasto administrativo
   (SeccionGastoCategoriaGasto; por defecto ADMINISTRATIVO).

No se migran los montos en 0 ni las monedas distintas de PEN/USD (no cumplen las
reglas de GastoDirecto). Reentrante: solo toma lo que aún no está en
contable.GastoDirectoLegacyMap y cada objeto valida su propia ausencia.
===============================================================================
*/
-- Solo corre sobre el esquema posterior a V2.3.x (GastoDirecto y su mapa) y V2.2.0
-- (CentroCosto.IdProyecto). EXEC dinámico: el cuerpo solo se compila si existen.
IF OBJECT_ID(N'contable.GastoDirectoLegacyMap', N'U') IS NULL
   OR OBJECT_ID(N'ControlPresupuestario.SeccionGastoCategoriaGasto', N'U') IS NULL
   OR COL_LENGTH(N'ControlPresupuestario.CentroCosto', N'IdProyecto') IS NULL
   OR COL_LENGTH(N'ControlPresupuestario.CatalogoPartida', N'IdSeccionGasto') IS NULL
    RETURN;

EXEC sys.sp_executesql N'
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Pendientes TABLE
(
    Origen VARCHAR(30) NOT NULL,
    IdLegacy INT NOT NULL,
    IdProyecto INT NULL,
    CodigoSeccion VARCHAR(30) NOT NULL,
    IdMoneda INT NOT NULL,
    CodigoMoneda VARCHAR(3) NOT NULL,
    IdProveedor INT NULL,
    Fecha DATE NOT NULL,
    Concepto NVARCHAR(250) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    Monto DECIMAL(18,2) NOT NULL,
    Estado VARCHAR(20) NOT NULL,
    FechaCreacion DATETIME2(0) NULL,
    FechaActualizacion DATETIME2(0) NULL,
    IdCentroCosto INT NULL,
    IdPresupuestoDetalle INT NULL,
    PRIMARY KEY (Origen, IdLegacy)
);

INSERT INTO @Pendientes (Origen, IdLegacy, IdProyecto, CodigoSeccion, IdMoneda, CodigoMoneda, IdProveedor, Fecha,
    Concepto, Descripcion, Monto, Estado, FechaCreacion, FechaActualizacion)
SELECT ''GASTO_PROYECTO'', gp.IdGastoProyecto, gp.IdProyecto,
    CASE LOWER(REPLACE(REPLACE(LTRIM(RTRIM(gp.TipoModulo)), ''-'', ''''), '' '', ''''))
        WHEN ''terreno'' THEN ''TERRENO''
        WHEN ''marketing'' THEN ''MARKETING_VENTAS''
        WHEN ''marketingpublicidad'' THEN ''MARKETING_VENTAS''
        WHEN ''gastosmunicipales'' THEN ''MUNICIPAL''
        WHEN ''gastosmunicipalesdistritales'' THEN ''MUNICIPAL''
        ELSE ''OTROS'' END,
    m.IdMoneda, m.Codigo, pm.IdProveedor, gp.Fecha,
    LEFT(COALESCE(NULLIF(LTRIM(RTRIM(gp.Concepto)), N''''), N''GASTO DEL PROYECTO''), 250), gp.Descripcion,
    CASE WHEN m.Codigo = ''USD'' THEN gp.MontoDolares ELSE gp.MontoSoles END,
    CASE WHEN gp.Activo = 1 AND gp.Estado = ''Activo'' THEN ''REGISTRADO'' ELSE ''ANULADO'' END,
    CONVERT(DATETIME2(0), gp.FechaCreacion), CONVERT(DATETIME2(0), gp.FechaActualizacion)
FROM contable.GastoProyecto gp
JOIN maestra.Moneda m ON m.Codigo = UPPER(LTRIM(RTRIM(gp.Moneda)))
LEFT JOIN maestra.ProveedorLegacyMap pm ON pm.Origen = ''PROVEEDOR_TERRENO'' AND pm.IdLegacy = gp.IdProveedorTerreno
WHERE m.Codigo IN (''PEN'', ''USD'')
  AND CASE WHEN m.Codigo = ''USD'' THEN gp.MontoDolares ELSE gp.MontoSoles END > 0
  AND NOT EXISTS (SELECT 1 FROM contable.GastoDirectoLegacyMap x
                  WHERE x.Origen = ''GASTO_PROYECTO'' AND x.IdLegacy = gp.IdGastoProyecto);

INSERT INTO @Pendientes (Origen, IdLegacy, IdProyecto, CodigoSeccion, IdMoneda, CodigoMoneda, IdProveedor, Fecha,
    Concepto, Descripcion, Monto, Estado, FechaCreacion, FechaActualizacion)
SELECT ''GASTO_ADMIN'', ga.IdGastoAdministrativo, ga.IdProyecto,
    COALESCE((SELECT TOP (1) sg.Codigo FROM ControlPresupuestario.SeccionGastoCategoriaGasto x
              JOIN ControlPresupuestario.SeccionGasto sg ON sg.IdSeccionGasto = x.IdSeccionGasto
              WHERE x.IdCategoriaGasto = ga.IdCategoriaGasto ORDER BY sg.Orden), ''ADMINISTRATIVO''),
    m.IdMoneda, m.Codigo,
    CASE WHEN EXISTS (SELECT 1 FROM maestra.Proveedor p2 WHERE p2.IdProveedor = COALESCE(pm.IdProveedor, ga.IdProveedor))
         THEN COALESCE(pm.IdProveedor, ga.IdProveedor) END,
    ga.Fecha, LEFT(COALESCE(cg.Nombre, N''GASTO ADMINISTRATIVO''), 250), ga.Descripcion, ga.Monto,
    CASE WHEN ga.Activo = 1 THEN ''REGISTRADO'' ELSE ''ANULADO'' END,
    CONVERT(DATETIME2(0), ga.FechaCreacion), NULL
FROM contable.GastoAdministrativo ga
JOIN maestra.Moneda m ON m.Codigo = UPPER(LTRIM(RTRIM(ga.Moneda)))
LEFT JOIN maestra.CategoriaGasto cg ON cg.IdCategoriaGasto = ga.IdCategoriaGasto
LEFT JOIN maestra.ProveedorLegacyMap pm
    ON pm.Origen = ''PROVEEDOR_GASTO_ADMIN'' AND pm.IdLegacy = ga.IdProveedorGastoAdministrativo
WHERE m.Codigo IN (''PEN'', ''USD'')
  AND ga.Monto > 0
  AND NOT EXISTS (SELECT 1 FROM contable.GastoDirectoLegacyMap x
                  WHERE x.Origen = ''GASTO_ADMIN'' AND x.IdLegacy = ga.IdGastoAdministrativo);

IF EXISTS (SELECT 1 FROM @Pendientes)
BEGIN
    DECLARE @Fecha DATETIME2(0) = SYSDATETIME();
    DECLARE @IdAprobado INT = (SELECT IdEstadoPresupuesto FROM ControlPresupuestario.EstadoPresupuesto WHERE Codigo = ''APROBADO'');
    DECLARE @IdTipoProyecto INT = (SELECT IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = ''PROYECTO'');
    DECLARE @IdTipoAdmin INT = (SELECT IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = ''ADMINISTRACION'');
    DECLARE @IdTipoIndirectos INT = COALESCE(
        (SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = ''INDIRECTOS''),
        (SELECT MIN(IdTipoPartida) FROM ControlPresupuestario.TipoPartida));
    DECLARE @IdTipoAdministrativos INT = COALESCE(
        (SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = ''ADMINISTRATIVOS''), @IdTipoIndirectos);
    IF @IdAprobado IS NULL OR @IdTipoProyecto IS NULL OR @IdTipoAdmin IS NULL OR @IdTipoIndirectos IS NULL
        THROW 50000, ''V2_6_3: faltan catálogos base (estado APROBADO, tipos de centro de costo o de partida).'', 1;

    -- 1) Catálogo: HIST y una hoja por sección usada.
    IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.CatalogoPartida WHERE Codigo = ''HIST'')
        INSERT INTO ControlPresupuestario.CatalogoPartida (Codigo, Nombre, Descripcion, IdTipoPartida, Nivel, Activo)
        VALUES (''HIST'', N''Gastos históricos (migrados)'',
            N''Gastos de las pantallas antiguas sin partida presupuestal. Solo lectura.'', @IdTipoIndirectos, 1, 1);
    DECLARE @IdRaiz INT = (SELECT IdCatalogoPartida FROM ControlPresupuestario.CatalogoPartida WHERE Codigo = ''HIST'');

    INSERT INTO ControlPresupuestario.CatalogoPartida (Codigo, Nombre, Descripcion, IdPartidaPadre, IdTipoPartida, Nivel, Activo, IdSeccionGasto)
    SELECT ''HIST.'' + sg.Codigo, LEFT(N''Históricos - '' + sg.Nombre, 200), N''Gastos históricos migrados de la sección.'',
        @IdRaiz, CASE WHEN sg.Codigo = ''ADMINISTRATIVO'' THEN @IdTipoAdministrativos ELSE @IdTipoIndirectos END, 2, 1,
        sg.IdSeccionGasto
    FROM ControlPresupuestario.SeccionGasto sg
    WHERE sg.Codigo IN (SELECT DISTINCT CodigoSeccion FROM @Pendientes)
      AND NOT EXISTS (SELECT 1 FROM ControlPresupuestario.CatalogoPartida c WHERE c.Codigo = ''HIST.'' + sg.Codigo);

    -- 2) Centros de costo: el del proyecto (se crea si falta) o el administrativo.
    INSERT INTO ControlPresupuestario.CentroCosto (Codigo, Nombre, IdTipoCentroCosto, Descripcion, Activo, IdProyecto)
    SELECT ''CC-PRY-'' + CONVERT(VARCHAR(10), pr.IdProyecto), LEFT(pr.NombreProyecto, 150), @IdTipoProyecto,
        N''Creado por la migración de gastos históricos (V2.6.3).'', 1, pr.IdProyecto
    FROM maestra.Proyecto pr
    WHERE pr.IdProyecto IN (SELECT IdProyecto FROM @Pendientes WHERE IdProyecto IS NOT NULL)
      AND NOT EXISTS (SELECT 1 FROM ControlPresupuestario.CentroCosto cc WHERE cc.IdProyecto = pr.IdProyecto)
      AND NOT EXISTS (SELECT 1 FROM ControlPresupuestario.CentroCosto cc
                      WHERE cc.Codigo = ''CC-PRY-'' + CONVERT(VARCHAR(10), pr.IdProyecto));

    UPDATE p SET IdCentroCosto = cc.IdCentroCosto
    FROM @Pendientes p
    JOIN ControlPresupuestario.CentroCosto cc ON cc.IdProyecto = p.IdProyecto;

    IF EXISTS (SELECT 1 FROM @Pendientes WHERE IdCentroCosto IS NULL)
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.CentroCosto WHERE IdTipoCentroCosto = @IdTipoAdmin)
            INSERT INTO ControlPresupuestario.CentroCosto (Codigo, Nombre, IdTipoCentroCosto, Descripcion, Activo)
            VALUES (''CC-ADM-HIST'', N''Administración (gastos históricos)'', @IdTipoAdmin,
                N''Creado por la migración de gastos históricos (V2.6.3).'', 1);
        UPDATE @Pendientes SET IdCentroCosto =
            (SELECT TOP (1) IdCentroCosto FROM ControlPresupuestario.CentroCosto
             WHERE IdTipoCentroCosto = @IdTipoAdmin ORDER BY Activo DESC, IdCentroCosto)
        WHERE IdCentroCosto IS NULL;
    END;

    -- 3) Presupuesto histórico inactivo por centro y moneda, con su versión 1 aprobada.
    INSERT INTO ControlPresupuestario.Presupuesto (IdCentroCosto, IdMoneda, Codigo, Nombre, Descripcion, Activo)
    SELECT DISTINCT p.IdCentroCosto, p.IdMoneda,
        ''HIST-'' + CONVERT(VARCHAR(10), p.IdCentroCosto) + ''-'' + p.CodigoMoneda,
        LEFT(N''Gastos históricos (migrados) - '' + cc.Nombre + N'' '' + p.CodigoMoneda, 200),
        N''Archivo de gastos anteriores al control presupuestario. Inactivo: no entra en reportes ni admite gastos nuevos.'', 0
    FROM @Pendientes p
    JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
    WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.Presupuesto x
                      WHERE x.Codigo = ''HIST-'' + CONVERT(VARCHAR(10), p.IdCentroCosto) + ''-'' + p.CodigoMoneda);

    INSERT INTO ControlPresupuestario.PresupuestoVersion (IdPresupuesto, IdEstadoPresupuesto, NumeroVersion, Descripcion,
        FechaCreacion, FechaAprobacion, UsuarioCreacion, UsuarioAprobacion)
    SELECT pr.IdPresupuesto, @IdAprobado, 1, N''Gastos históricos migrados'', @Fecha, @Fecha, N''MIGRACION'', N''MIGRACION''
    FROM ControlPresupuestario.Presupuesto pr
    WHERE pr.Codigo LIKE ''HIST-%''
      AND NOT EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoVersion v WHERE v.IdPresupuesto = pr.IdPresupuesto);

    INSERT INTO ControlPresupuestario.PresupuestoDetalle (IdPresupuestoVersion, IdCatalogoPartida, MontoPresupuestado, Observacion)
    SELECT DISTINCT v.IdPresupuestoVersion, c.IdCatalogoPartida, 0,
        N''Partida de gastos históricos migrados; no admite gastos nuevos.''
    FROM @Pendientes p
    JOIN ControlPresupuestario.Presupuesto pr
        ON pr.Codigo = ''HIST-'' + CONVERT(VARCHAR(10), p.IdCentroCosto) + ''-'' + p.CodigoMoneda
    JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuesto = pr.IdPresupuesto AND v.NumeroVersion = 1
    JOIN ControlPresupuestario.CatalogoPartida c ON c.Codigo = ''HIST.'' + p.CodigoSeccion
    WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle d
                      WHERE d.IdPresupuestoVersion = v.IdPresupuestoVersion AND d.IdCatalogoPartida = c.IdCatalogoPartida);

    UPDATE p SET IdPresupuestoDetalle = d.IdPresupuestoDetalle
    FROM @Pendientes p
    JOIN ControlPresupuestario.Presupuesto pr
        ON pr.Codigo = ''HIST-'' + CONVERT(VARCHAR(10), p.IdCentroCosto) + ''-'' + p.CodigoMoneda
    JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuesto = pr.IdPresupuesto AND v.NumeroVersion = 1
    JOIN ControlPresupuestario.CatalogoPartida c ON c.Codigo = ''HIST.'' + p.CodigoSeccion
    JOIN ControlPresupuestario.PresupuestoDetalle d
        ON d.IdPresupuestoVersion = v.IdPresupuestoVersion AND d.IdCatalogoPartida = c.IdCatalogoPartida;

    IF EXISTS (SELECT 1 FROM @Pendientes WHERE IdPresupuestoDetalle IS NULL)
        THROW 50000, ''V2_6_3: hay gastos históricos sin partida histórica (sección de gasto inexistente).'', 1;

    -- 4) Gastos, mapa y documentos.
    DECLARE @Migrados TABLE (Origen VARCHAR(30) NOT NULL, IdLegacy INT NOT NULL, IdGastoDirecto INT NOT NULL);
    MERGE contable.GastoDirecto AS destino
    USING @Pendientes AS fuente ON 1 = 0
    WHEN NOT MATCHED THEN INSERT
        (IdPresupuestoDetalle, IdProveedor, IdMoneda, Fecha, Concepto, Descripcion, Monto, Estado, FechaCreacion, FechaActualizacion)
    VALUES
        (fuente.IdPresupuestoDetalle, fuente.IdProveedor, fuente.IdMoneda, fuente.Fecha, fuente.Concepto,
         fuente.Descripcion, fuente.Monto, fuente.Estado, COALESCE(fuente.FechaCreacion, @Fecha), fuente.FechaActualizacion)
    OUTPUT fuente.Origen, fuente.IdLegacy, inserted.IdGastoDirecto INTO @Migrados (Origen, IdLegacy, IdGastoDirecto);

    INSERT INTO contable.GastoDirectoLegacyMap (Origen, IdLegacy, IdGastoDirecto)
    SELECT Origen, IdLegacy, IdGastoDirecto FROM @Migrados;

    INSERT INTO contable.GastoDirectoDocumento (IdGastoDirecto, TipoDocumento, NombreArchivo, RutaArchivo, Extension, FechaCreacion)
    SELECT m.IdGastoDirecto, ''Factura'', d.NombreArchivo, d.RutaArchivo, d.Extension, CONVERT(DATETIME2(0), d.FechaCreacion)
    FROM contable.GastoProyectoDocumento d
    JOIN @Migrados m ON m.Origen = ''GASTO_PROYECTO'' AND m.IdLegacy = d.IdGastoProyecto;

    INSERT INTO contable.GastoDirectoDocumento (IdGastoDirecto, TipoDocumento, NombreArchivo, RutaArchivo, Extension, FechaCreacion)
    SELECT m.IdGastoDirecto, CASE WHEN d.TipoDocumento = ''Pago'' THEN ''Pago'' ELSE ''Factura'' END,
        d.NombreArchivo, d.RutaArchivo, d.Extension, CONVERT(DATETIME2(0), d.FechaCreacion)
    FROM contable.GastoAdministrativoDocumento d
    JOIN @Migrados m ON m.Origen = ''GASTO_ADMIN'' AND m.IdLegacy = d.IdGastoAdministrativo;
END;

COMMIT TRANSACTION;
';
